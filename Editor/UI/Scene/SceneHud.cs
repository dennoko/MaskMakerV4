using System;
using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// SceneView に常駐する UI（開始バッジ・ラジアルメニュー・トースト・ヒント。UI 設計 §5.2 / §6.2 / §2.6.3）。
    /// 各 SceneView の rootVisualElement に 1 つだけルートを置く。同名の物が残っていれば先に外す。
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneHud
    {
        private const string RootName = "UVMaskMaker-Hud";

        private sealed class Hud
        {
            public SceneView View;
            public VisualElement Root;
            public Button Badge;
            public VisualElement Toast;
            public Label ToastText;
            public Image ToastIcon;
            public Button ToastAction;
            public Label Hint;
            public IVisualElementScheduledItem ToastHide;
            public IVisualElementScheduledItem HintHide;
        }

        private static readonly Dictionary<SceneView, Hud> Huds = new Dictionary<SceneView, Hud>();
        private static double _nextPoll;

        // ラジアル
        private struct Slice
        {
            public string Icon;
            public string Fallback;
            public string NameKey;
            public string Key;
            public Action Run;
            public Func<bool> Current;
        }

        private static VisualElement _radial;
        private static Label _radialCenter;
        private static readonly List<VisualElement> _sliceElements = new List<VisualElement>();
        private static Slice[] _slices;
        private static Vector2 _radialCenterPos;
        private static int _hot = -1;
        private static Hud _radialHud;
        private static double _radialStill;

        static SceneHud()
        {
            EditorApplication.update += Poll;
            Notifier.Posted += OnMessage;
            Selection.selectionChanged += RefreshAll;
            UiHub.Changed += RefreshAll;
        }

        // ── 生成 ──

        private static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.5;

            foreach (SceneView view in SceneView.sceneViews)
                if (view != null && (!Huds.TryGetValue(view, out var h) || h.Root.panel == null)) Attach(view);
            var dead = new List<SceneView>();
            foreach (var kv in Huds) if (kv.Key == null) dead.Add(kv.Key);
            foreach (var d in dead) Huds.Remove(d);

            var s = SessionController.Current;
            string hint = Hints.Current(s);
            if (hint != null && s?.Interaction.PointerView != null && Huds.TryGetValue(s.Interaction.PointerView, out var hud))
                ShowHint(hud, hint);
        }

        private static void Attach(SceneView view)
        {
            var parent = view.rootVisualElement;
            parent.Q(RootName)?.RemoveFromHierarchy();
            var root = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore };
            UiAssets.SetupRoot(root, false);
            root.AddToClassList("mm-hud");
            parent.Add(root);

            var hud = new Hud { View = view, Root = root };
            hud.Badge = new Button(StartFromSelection) { text = "MaskMaker V4 ▶" };
            hud.Badge.AddToClassList("mm-start-badge");
            hud.Badge.AddToClassList("dennoko-button-secondary");
            root.Add(hud.Badge);

            hud.Toast = new VisualElement();
            hud.Toast.AddToClassList("mm-toast");
            hud.ToastIcon = new Image();
            hud.ToastIcon.AddToClassList("mm-toast-icon");
            hud.ToastText = new Label();
            hud.ToastAction = new Button { text = string.Empty };
            hud.ToastAction.AddToClassList("dennoko-button-secondary");
            hud.Toast.Add(hud.ToastIcon);
            hud.Toast.Add(hud.ToastText);
            hud.Toast.Add(hud.ToastAction);
            Bind.Show(hud.Toast, false);
            root.Add(hud.Toast);

            hud.Hint = new Label { pickingMode = PickingMode.Ignore };
            hud.Hint.AddToClassList("mm-cursor-hint");
            Bind.Show(hud.Hint, false);
            root.Add(hud.Hint);

            Huds[view] = hud;
            Refresh(hud);
        }

        public static void RefreshAll()
        {
            foreach (var h in Huds.Values) if (h.Root != null) Refresh(h);
        }

        private static void Refresh(Hud hud)
        {
            bool idle = !SessionController.IsWorking;
            Bind.Show(hud.Badge, idle && TargetResolver.FromSelection() != null);
            hud.Badge.text = L10n.Japanese ? "MaskMaker V4 で編集 ▶" : "Edit with MaskMaker V4 ▶";
        }

        private static void StartFromSelection()
        {
            var r = TargetResolver.FromSelection();
            if (r == null) return;
            MaskMakerWindow.EnsureOpen();
            SessionController.Start(r, origin: MessageOrigin.Scene);
        }

        /// <summary>SceneView の GUI 座標 → rootVisualElement の座標（上のツールバーの分だけずらす）。</summary>
        private static Vector2 ToRoot(SceneView view, Vector2 gui)
        {
            float camHeight = view.camera != null ? view.camera.pixelHeight / EditorGUIUtility.pixelsPerPoint : view.position.height;
            float offset = Mathf.Max(0f, view.rootVisualElement.layout.height - camHeight);
            return gui + new Vector2(0f, offset);
        }

        // ── トースト（シーンから出した時の通知。UI 設計 §6.2） ──

        private static void OnMessage(UserMessage m)
        {
            if (m.Origin != MessageOrigin.Scene || !SessionController.IsWorking) return;
            var s = SessionController.Current;
            var view = s.Interaction.PointerView ?? SceneView.lastActiveSceneView;
            if (view == null || !Huds.TryGetValue(view, out var hud)) return;

            hud.ToastText.text = m.Text;
            hud.ToastIcon.image = m.Level == MessageLevel.Success ? Icons.Get(Icons.Export) : Icons.ForLevel(m.Level);
            hud.ToastText.EnableInClassList("dennoko-text-warning", m.Level == MessageLevel.Warning);
            hud.ToastText.EnableInClassList("dennoko-text-error", m.Level == MessageLevel.Error);
            Bind.Show(hud.ToastAction, m.Action != null);
            if (m.Action != null)
            {
                hud.ToastAction.text = m.ActionLabel;
                var action = m.Action;
                var anchor = hud.ToastAction;
                hud.ToastAction.clickable = new Clickable(() =>
                {
                    action(anchor);
                    Bind.Show(anchor, false);
                });
            }
            var path = m.AssetPath;
            hud.ToastText.UnregisterCallback<ClickEvent>(PingToast);
            hud.ToastText.userData = path;
            hud.ToastText.RegisterCallback<ClickEvent>(PingToast);

            var pos = ToRoot(view, s.Interaction.PointerGui) + new Vector2(18f, 18f);
            hud.Toast.style.left = pos.x;
            hud.Toast.style.top = pos.y;
            Bind.Show(hud.Toast, true);
            hud.ToastHide?.Pause();
            hud.ToastHide = hud.Toast.schedule.Execute(() =>
            {
                if (hud.Toast.ContainsPoint(hud.Toast.WorldToLocal(PointerWorld(hud)))) return;
                Bind.Show(hud.Toast, false);
                hud.ToastHide.Pause();
            }).Every(500).StartingIn(m.Level == MessageLevel.Error ? 6000 : 4000);
        }

        private static Vector2 PointerWorld(Hud hud)
        {
            var s = SessionController.Current;
            if (s == null) return new Vector2(-1e5f, -1e5f);
            return hud.Root.LocalToWorld(ToRoot(hud.View, s.Interaction.PointerGui));
        }

        private static void PingToast(ClickEvent e)
        {
            if (!((e.currentTarget as VisualElement)?.userData is string path) || string.IsNullOrEmpty(path)) return;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj == null) return;
            if (e.clickCount >= 2) EditorUtility.RevealInFinder(path);
            else EditorGUIUtility.PingObject(obj);
        }

        private static void ShowHint(Hud hud, string text)
        {
            var s = SessionController.Current;
            hud.Hint.text = text;
            var pos = ToRoot(hud.View, s.Interaction.PointerGui) + new Vector2(18f, -26f);
            hud.Hint.style.left = pos.x;
            hud.Hint.style.top = pos.y;
            Bind.Show(hud.Hint, true);
            hud.HintHide?.Pause();
            hud.HintHide = hud.Hint.schedule.Execute(() => Bind.Show(hud.Hint, false)).StartingIn(3000);
        }

        // ── ラジアルメニュー（C 長押し。UI 設計 §5.2） ──

        public static void OpenRadial()
        {
            var s = SessionController.Current;
            if (s == null || _radial != null) return;
            var view = s.Interaction.PointerView ?? SceneView.lastActiveSceneView;
            if (view == null || !Huds.TryGetValue(view, out var hud)) return;

            _slices = BuildSlices();
            _radialHud = hud;
            _radialCenterPos = ToRoot(view, s.Interaction.PointerGui);
            _hot = -1;
            _radialStill = EditorApplication.timeSinceStartup;

            _radial = new VisualElement { pickingMode = PickingMode.Ignore };
            _radial.AddToClassList("mm-radial-menu");
            _radial.style.left = _radialCenterPos.x - 90f;
            _radial.style.top = _radialCenterPos.y - 90f;
            _radialCenter = new Label { pickingMode = PickingMode.Ignore };
            _radialCenter.AddToClassList("mm-radial-center");
            _radial.Add(_radialCenter);
            _sliceElements.Clear();
            for (int i = 0; i < 8; i++)
            {
                var sl = _slices[i];
                var e = new Button { pickingMode = PickingMode.Ignore, text = string.Empty };
                e.AddToClassList("mm-radial-slice");
                float ang = i * 45f * Mathf.Deg2Rad;
                e.style.left = 90f + Mathf.Cos(ang) * 64f - 18f;
                e.style.top = 90f - Mathf.Sin(ang) * 64f - 18f;
                if (sl.Run == null) e.style.opacity = 0.25f;
                else
                {
                    if (sl.Icon != null) Icons.Apply(e, sl.Icon, sl.Fallback, null);
                    else e.text = sl.Fallback;
                    Bind.Active(e, sl.Current != null && sl.Current());
                }
                var key = new Label(sl.Key ?? string.Empty) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("mm-radial-key");
                Bind.Show(key, false);
                e.Add(key);
                _radial.Add(e);
                _sliceElements.Add(e);
            }
            hud.Root.Add(_radial);
            hud.Root.schedule.Execute(ShowKeysIfStill).StartingIn(600);
            s.Interaction.RadialOpen = true;
        }

        private static void ShowKeysIfStill()
        {
            if (_radial == null || EditorApplication.timeSinceStartup - _radialStill < 0.55) return;
            foreach (var e in _sliceElements)
                foreach (var child in e.Children())
                    if (child is Label) Bind.Show(child, true);
        }

        public static void OnPointerMoved(SceneView view, Vector2 gui)
        {
            if (_radial == null || _radialHud == null || _radialHud.View != view) return;
            var d = ToRoot(view, gui) - _radialCenterPos;
            int hot = -1;
            if (d.magnitude >= 18f)
            {
                float ang = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
                if (ang < 0) ang += 360f;
                hot = Mathf.RoundToInt(ang / 45f) % 8;
                if (_slices[hot].Run == null) hot = -1;
            }
            if (hot == _hot) return;
            _hot = hot;
            _radialStill = EditorApplication.timeSinceStartup;
            for (int i = 0; i < _sliceElements.Count; i++) _sliceElements[i].EnableInClassList("mm-radial-hot", i == hot);
            _radialCenter.text = hot >= 0 ? L10n.T(_slices[hot].NameKey) : string.Empty;
        }

        public static void CloseRadial(bool apply)
        {
            if (_radial == null) return;
            int hot = _hot;
            _radial.RemoveFromHierarchy();
            _radial = null;
            _radialHud = null;
            var s = SessionController.Current;
            if (s != null) s.Interaction.RadialOpen = false;
            if (apply && hot >= 0) _slices[hot].Run?.Invoke();
        }

        /// <summary>0 = →, 1 = ↗, 2 = ↑, 3 = ↖, 4 = ←, 5 = ↙, 6 = ↓, 7 = ↘。↑・↗・↖ は両モード共通。</summary>
        private static Slice[] BuildSlices()
        {
            bool paint = UiRules.PaintMode;
            var s = new Slice[8];
            s[2] = paint
                ? new Slice { Icon = Icons.Select, Fallback = "S", NameKey = "term.select", Key = "Tab", Run = () => SessionCommands.SetPaintMode(false) }
                : new Slice { Icon = Icons.Paint, Fallback = "P", NameKey = "term.paint", Key = "Tab", Run = () => SessionCommands.SetPaintMode(true) };
            s[1] = new Slice { Icon = Icons.Symmetry, Fallback = "X", NameKey = "term.symmetry", Key = "X", Run = SessionCommands.ToggleSymmetry, Current = () => SettingsService.Get(SettingKeys.SymmetryEnabled) };
            s[3] = new Slice { Icon = Icons.Clear, Fallback = "C", NameKey = "term.clear", Run = () => SessionCommands.ClearAll(MessageOrigin.Scene) };
            if (!paint)
            {
                var g = SettingsService.Get(SettingKeys.Granularity);
                s[4] = new Slice { Fallback = "All", NameKey = "term.selectAll", Key = "Ctrl+A", Run = SessionCommands.SelectAll };
                s[0] = Unit(SelectionGranularity.UVIsland, "term.island", "1", g);
                s[7] = Unit(SelectionGranularity.ConnectedMesh, "term.part", "2", g);
                s[6] = Unit(SelectionGranularity.Polygon, "term.face", "3", g);
                s[5] = new Slice { Fallback = "Inv", NameKey = "term.invert", Key = "Ctrl+I", Run = SessionCommands.InvertSelection };
            }
            else
            {
                var t = SettingsService.Get(SettingKeys.PaintTool);
                s[4] = new Slice { NameKey = string.Empty };
                s[0] = Tool(PaintSubMode.Brush, Icons.Paint, "term.brush", "B", t);
                s[7] = Tool(PaintSubMode.Rect, Icons.Rect, "term.rect", "M", t);
                s[6] = Tool(PaintSubMode.Lasso, Icons.Lasso, "term.lasso", "L", t);
                s[5] = Tool(PaintSubMode.Eraser, Icons.Eraser, "term.eraser", "E", t);
            }
            return s;
        }

        private static Slice Unit(SelectionGranularity g, string nameKey, string key, SelectionGranularity current)
            => new Slice { Fallback = L10n.T(nameKey), NameKey = nameKey, Key = key, Run = () => SessionCommands.SetGranularity(g), Current = () => current == g };

        private static Slice Tool(PaintSubMode t, string icon, string nameKey, string key, PaintSubMode current)
            => new Slice { Icon = icon, Fallback = key, NameKey = nameKey, Key = key, Run = () => SessionCommands.SetPaintTool(t), Current = () => current == t };
    }
}
