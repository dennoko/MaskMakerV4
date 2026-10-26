using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 対象カード（UI 設計 §4.3）。Idle: 対象欄と横の「編集 ▶」（親を選ぶと子 Renderer の一覧）。
    /// Working: 対象名のドロップダウン（同じアバター内の切替）、終了、サブメッシュ / UV（複数ある時だけ）。
    /// </summary>
    internal sealed class TargetCard
    {
        /// <summary>Idle で開始ボタンが使う対象（一覧で選んだもの、または選択から自動入力したもの）。</summary>
        public static Renderer PendingTarget { get; private set; }

        private readonly VisualElement _root;
        private readonly ObjectField _field;
        private readonly VisualElement _list;
        private List<Renderer> _siblings = new List<Renderer>();

        public TargetCard(VisualElement root)
        {
            _root = root;
            _field = root.Q<ObjectField>("target-field");
            _field.objectType = typeof(Renderer);
            _field.label = string.Empty;
            _list = root.Q("target-list");
            _field.RegisterValueChangedCallback(e => SetPending(e.newValue as Renderer));

            var edit = root.Q<Button>("edit-button");
            edit.text = L10n.T("button.edit");
            edit.tooltip = L10n.T("tooltip.edit");
            edit.clicked += () => SessionController.Start(PendingTarget);
            UiHub.Watch(edit, () => edit.SetEnabled(PendingTarget != null));

            root.Q<Button>("end-button").text = L10n.T("button.end");
            root.Q<Button>("end-button").clicked += () => SessionController.End();

            var targetDrop = root.Q<DropdownField>("target-dropdown");
            targetDrop.RegisterValueChangedCallback(e =>
            {
                int i = targetDrop.index;
                if (i >= 0 && i < _siblings.Count) SessionController.SwitchTarget(_siblings[i]);
            });

            var submesh = root.Q<DropdownField>("submesh-dropdown");
            submesh.RegisterValueChangedCallback(_ => SettingsService.Set(SettingKeys.TargetSubmesh, submesh.index - 1));

            var uv = root.Q<DropdownField>("uv-dropdown");
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
            if (SessionController.IsWorking) return; // 作業中は選択の変化を無視（切替はドロップダウン）
            var go = Selection.activeGameObject;
            var candidates = TargetResolver.Candidates(go);
            SetPending(candidates.Count > 0 ? TargetResolver.Best(candidates) : null, candidates);
        }

        private void SetPending(Renderer r, List<Renderer> candidates = null)
        {
            PendingTarget = r;
            _field.SetValueWithoutNotify(r);
            BuildList(candidates != null && candidates.Count > 1 ? candidates : null);
            UiHub.Raise();
        }

        private void BuildList(List<Renderer> candidates)
        {
            _list.Clear();
            if (candidates == null) return;
            foreach (var r in candidates)
            {
                var renderer = r;
                var row = new Button(() => SessionController.Start(renderer)) { text = string.Empty };
                row.AddToClassList("mm-target-row");
                var icon = new Image { image = Icons.ForRenderer(r) };
                icon.AddToClassList("mm-target-icon");
                row.Add(icon);
                row.Add(new Label(r.name));
                Bind.Active(row, r == PendingTarget);
                _list.Add(row);
            }
        }

        private void Refresh()
        {
            var s = SessionController.Current;
            Bind.Show(_root.Q("target-idle"), s == null);
            Bind.Show(_root.Q("target-working"), s != null);
            if (s == null)
            {
                _field.label = string.Empty;
                return;
            }

            var icon = _root.Q("target-icon");
            icon.style.backgroundImage = Icons.ForRenderer(s.Target);

            _siblings = TargetResolver.Siblings(s.Target);
            if (!_siblings.Contains(s.Target)) _siblings.Insert(0, s.Target);
            var targetDrop = _root.Q<DropdownField>("target-dropdown");
            var names = new List<string>();
            foreach (var r in _siblings) names.Add(r.name);
            targetDrop.choices = names;
            targetDrop.SetValueWithoutNotify(s.Target.name);

            var submesh = _root.Q<DropdownField>("submesh-dropdown");
            Bind.Show(submesh, UiRules.ShowSubmesh(s));
            var subNames = new List<string> { L10n.T("term.all") };
            var mats = s.Target.sharedMaterials;
            for (int i = 0; i < s.Mesh.SubmeshCount; i++)
                subNames.Add(i < mats.Length && mats[i] != null ? mats[i].name : "#" + i);
            submesh.choices = subNames;
            int sub = s.ActiveSubmesh;
            submesh.SetValueWithoutNotify(subNames[Mathf.Clamp(sub + 1, 0, subNames.Count - 1)]);

            var uv = _root.Q<DropdownField>("uv-dropdown");
            var channels = UiRules.UVChannels(s);
            Bind.Show(uv, channels.Count > 1);
            var uvNames = new List<string>();
            foreach (int c in channels) uvNames.Add("UV" + c);
            uv.choices = uvNames;
            uv.SetValueWithoutNotify("UV" + s.UVChannelUsed);
        }
    }
}
