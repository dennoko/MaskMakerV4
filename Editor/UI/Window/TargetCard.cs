using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// B1 対象カード（skill blocks/cards.md）。見出しは付けない（中身で対象と分かる）。
    ///   Idle    : 対象欄（Hierarchy の選択に追従して自動で入る）。親を選ぶと子 Renderer の候補一覧（クリックで決定、ダブルクリックで開始）
    ///   Working : 「アイコン + 名前 ▼」（▼ で同じアバター内の対象に切替）、サブメッシュ / UV（複数ある時だけ）、情報行
    /// 作業中は Hierarchy の選択を変えても対象を変えない（切り替えは ▼ から）。
    /// </summary>
    internal sealed class TargetCard
    {
        /// <summary>Idle で開始ボタンが使う対象（候補一覧で選んだもの、対象欄に入れたもの、または選択から自動入力したもの）。</summary>
        public static Renderer PendingTarget { get; private set; }

        private readonly VisualElement _root;
        private readonly ObjectField _field;
        private readonly VisualElement _candidates;

        public TargetCard(VisualElement root)
        {
            _root = root;
            _field = root.Q<ObjectField>("target-field");
            _field.objectType = typeof(Renderer);
            _field.label = string.Empty;
            _field.RegisterValueChangedCallback(e => SetPending(e.newValue as Renderer));
            _candidates = root.Q("target-candidates");
            root.Q<Label>("target-header").text = L10n.T("target.header");

            var targetSwitch = root.Q<Button>("target-switch");
            targetSwitch.tooltip = L10n.T("tooltip.switchTarget");
            targetSwitch.clicked += () => ShowSiblingMenu(targetSwitch);

            var submesh = root.Q<DropdownField>("target-submesh");
            submesh.tooltip = L10n.T("tooltip.submesh");
            submesh.RegisterValueChangedCallback(_ => SettingsService.Set(SettingKeys.TargetSubmesh, submesh.index - 1));

            var uv = root.Q<DropdownField>("target-uv");
            uv.tooltip = L10n.T("tooltip.uvChannel");
            uv.RegisterValueChangedCallback(e =>
            {
                var s = SessionController.Current;
                var channels = UiRules.UVChannels(s);
                if (uv.index < 0 || uv.index >= channels.Count) return;
                SettingsService.Set(SettingKeys.UVChannel, channels[uv.index]);
                SessionController.Restart();
            });

            // パネルにある間だけ選択の変化を受け取る。タブの切替・ドッキング・最大化でパネルから外れても、
            // 戻った時に購読し直し、外れている間に変わった選択を読み直す
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                SubscribeSelection();
                OnSelectionChanged();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= OnSelectionChanged);
            if (root.panel != null) SubscribeSelection();
            OnSelectionChanged();
            UiHub.Watch(root.Q("target-card"), Refresh);
        }

        private void SubscribeSelection()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (SessionController.IsWorking) return; // 作業中は選択の変化を無視（切替は ▼ から）
            var go = Selection.activeGameObject;
            var candidates = TargetResolver.Candidates(go);
            SetPending(candidates.Count > 0 ? TargetResolver.Best(candidates) : null, candidates);
        }

        private List<Renderer> _shownCandidates;

        private void SetPending(Renderer r, List<Renderer> candidates = null)
        {
            PendingTarget = r;
            _field.SetValueWithoutNotify(r);
            var before = _shownCandidates;
            if (candidates != null) _shownCandidates = candidates.Count > 1 ? candidates : null;
            else if (_shownCandidates != null && !_shownCandidates.Contains(r)) _shownCandidates = null;
            // 候補の中で選び直しただけなら作り直さない（ダブルクリックの 2 回目を同じ行で受けるため）
            if (_shownCandidates != before) BuildCandidates();
            else foreach (var row in _candidates.Children()) Bind.Active(row, row.userData as Renderer == r);
            UiHub.Raise();
        }

        /// <summary>ルートを選んだ時の子 Renderer の候補（クリックで決定、ダブルクリックで開始）。</summary>
        private void BuildCandidates()
        {
            _candidates.Clear();
            Bind.Show(_candidates, _shownCandidates != null);
            if (_shownCandidates == null) return;
            foreach (var r in _shownCandidates)
            {
                var renderer = r;
                var row = new Button { text = string.Empty, tooltip = L10n.T("tooltip.candidate"), userData = r };
                row.AddToClassList("dennoko-candidate");
                row.Add(RendererIcon(r));
                var name = new Label(r.name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("dennoko-candidate__name");
                row.Add(name);
                var mesh = TargetKey.SourceMesh(r);
                if (mesh != null)
                {
                    long indices = 0;
                    for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
                    var sub = new Label(L10n.F("info.triangles", (indices / 3).ToString("N0"))) { pickingMode = PickingMode.Ignore };
                    sub.AddToClassList("dennoko-candidate__sub");
                    row.Add(sub);
                }
                row.clickable = new Clickable(() => SetPending(renderer));
                row.RegisterCallback<ClickEvent>(e =>
                {
                    if (e.clickCount >= 2) SessionController.Start(renderer);
                });
                Bind.Active(row, r == PendingTarget);
                _candidates.Add(row);
            }
        }

        private static VisualElement RendererIcon(Renderer r)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("dennoko-icon");
            var tex = Icons.ForRenderer(r);
            if (tex != null) icon.style.backgroundImage = tex;
            return icon;
        }

        /// <summary>▼: 同じアバター内の Renderer に切り替える（今の作業は保存、切替先の前回作業を復元）。</summary>
        private static void ShowSiblingMenu(VisualElement anchor)
        {
            var s = SessionController.Current;
            if (s == null) return;
            var siblings = TargetResolver.Siblings(s.Target);
            if (!siblings.Contains(s.Target)) siblings.Insert(0, s.Target);
            var menu = new GenericDropdownMenu();
            foreach (var r in siblings)
            {
                var renderer = r;
                menu.AddItem(r.name, r == s.Target, () => SessionController.SwitchTarget(renderer));
            }
            menu.DropDown(anchor.worldBound, anchor, true);
        }

        private void Refresh()
        {
            var s = SessionController.Current;
            Bind.Show(_root.Q("target-idle"), s == null);
            Bind.Show(_candidates, s == null && _shownCandidates != null);
            Bind.Show(_root.Q("target-working"), s != null);
            var info = _root.Q("target-info");
            if (s == null)
            {
                Bind.Show(_root.Q("target-sub"), false);
                Bind.Show(info, false);
                return;
            }

            var icon = _root.Q("target-switch-icon");
            var tex = Icons.ForRenderer(s.Target);
            icon.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            _root.Q<Label>("target-switch-name").text = s.Target.name;

            var submesh = _root.Q<DropdownField>("target-submesh");
            bool showSub = UiRules.ShowSubmesh(s);
            Bind.Show(submesh, showSub);
            var subNames = new List<string> { L10n.T("term.all") };
            var mats = s.Target.sharedMaterials;
            for (int i = 0; i < s.Mesh.SubmeshCount; i++)
                subNames.Add(i < mats.Length && mats[i] != null ? mats[i].name : "#" + i);
            submesh.choices = subNames;
            int sub = s.ActiveSubmesh;
            submesh.SetValueWithoutNotify(subNames[Mathf.Clamp(sub + 1, 0, subNames.Count - 1)]);

            var uv = _root.Q<DropdownField>("target-uv");
            var channels = UiRules.UVChannels(s);
            bool showUv = channels.Count > 1;
            Bind.Show(uv, showUv);
            var uvNames = new List<string>();
            foreach (int c in channels) uvNames.Add("UV" + c);
            uv.choices = uvNames;
            uv.SetValueWithoutNotify("UV" + s.UVChannelUsed);

            Bind.Show(_root.Q("target-sub"), showSub || showUv);

            // 情報行: 三角形数 · サブメッシュ数 · 使っている UV
            info.Clear();
            info.Add(new Label(L10n.F("info.triangles", s.Analysis.TriangleCount.ToString("N0"))));
            if (s.Mesh.SubmeshCount > 1) info.Add(new Label(L10n.F("info.submeshes", s.Mesh.SubmeshCount)));
            info.Add(new Label("UV" + s.UVChannelUsed));
            Bind.Show(info, true);
        }
    }
}
