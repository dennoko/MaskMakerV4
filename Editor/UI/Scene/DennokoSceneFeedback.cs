using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// S4 シーンフィードバック (blocks/sceneview.md)。そのままコピーして使う。
    ///   トースト      : Toast()。シーンから実行したらカーソルの脇、それ以外は SceneView 下端の中央。4 秒、ホバー中は消えない
    ///   カーソルヒント : ShowHint() / HideHint()。カーソルに付いていき、操作したら消える
    ///   迷い検知ヒント : LostHintText を設定すると、開始後 3 秒シーンで操作が無いとき 1 行だけ出す (3 回まで)
    ///   修飾キーのバッジ: SetModifier()。Shift / Ctrl を押している間「+ / −」
    ///
    /// 各 SceneView の rootVisualElement に、透明な全面のルート (.dennoko-hud、pickingMode = Ignore) を 1 つずつ重ねる。
    /// SceneView は開き直し・ドック移動でルートを作り直すので、0.5 秒ごとに付け直す。
    /// </summary>
    [InitializeOnLoad]
    internal static class DennokoSceneFeedback
    {
        private const string RootName = "UVMaskMaker-SceneFeedback";
        private const double LostHintDelay = 3.0;
        private const int LostHintMax = 3;

        /// <summary>迷っていそうなときに出す 1 行 (例: () => "クリックで選択")。null なら出さない。</summary>
        public static Func<string> LostHintText;

        /// <summary>トーストの文字をクリックしたとき (例: 出力したファイルを Ping)。null なら何もしない。</summary>
        public static Action<int> ToastTextClicked;   // 引数はクリック回数 (2 = ダブルクリック)

        private sealed class Hud
        {
            public VisualElement Root;
            public VisualElement Toast;
            public VisualElement ToastIcon;
            public Label ToastText;
            public Button ToastAction;
            public Label Hint;
            public VisualElement Badge;
            public Label BadgeText;
            public IVisualElementScheduledItem ToastHide;
        }

        private static readonly Dictionary<SceneView, Hud> Huds = new Dictionary<SceneView, Hud>();
        private static double _nextPoll;
        private static SceneView _pointerView;
        private static Vector2 _pointerGui;
        private static int _lostHintCount;
        private static bool _hintFollowsCursor;
        private static Action _toastAction;

        static DennokoSceneFeedback()
        {
            EditorApplication.update += Poll;
            SceneView.duringSceneGui += OnSceneGui;
            DennokoSceneSession.Ended += _ =>
            {
                HideHint();
                foreach (var hud in Huds.Values) Show(hud.Badge, false);
            };
            DennokoSceneSession.ActiveChanged += () => _lostHintCount = 0;
        }

        // ─── トースト ─────────────────────────────────────────────────

        /// <summary>
        /// 1 行のトースト (30 字まで) を出す。操作ボタンは 1 つまで。
        /// atCursor = true はシーンから実行したとき (カーソルの脇)、false はウィンドウやパネルから実行したとき (下端の中央)。
        /// 成功・警告・エラーはアイコンの色だけで区別する (DennokoIcons.Success / Warning / Info)。
        /// </summary>
        public static void Toast(string text, string icon, string actionLabel = null, Action action = null, bool atCursor = true)
        {
            var view = atCursor && _pointerView != null ? _pointerView : SceneView.lastActiveSceneView;
            var hud = EnsureHud(view);
            if (hud == null) return;

            DennokoIcons.SetImage(hud.ToastIcon, icon);
            hud.ToastText.text = text;
            _toastAction = action;
            hud.ToastAction.text = actionLabel ?? string.Empty;
            Show(hud.ToastAction, actionLabel != null);
            Show(hud.Toast, true);

            if (atCursor && view == _pointerView)
            {
                Place(hud.Toast, ToRoot(view, _pointerGui) + new Vector2(16f, 12f));
                hud.Toast.style.bottom = StyleKeyword.Auto;
                hud.Toast.style.translate = new Translate(0, 0);
            }
            else
            {
                hud.Toast.style.left = Length.Percent(50);
                hud.Toast.style.top = StyleKeyword.Auto;
                hud.Toast.style.bottom = 16f;
                hud.Toast.style.translate = new Translate(Length.Percent(-50), 0);
            }
            ScheduleToastHide(hud, 4000);
            view.Repaint();
        }

        // ─── カーソルヒント / 修飾キーのバッジ ─────────────────────────

        /// <summary>カーソルの右下にヒントを出す。カーソルに付いていき、HideHint (または MarkInteracted) で消える。</summary>
        public static void ShowHint(string text)
        {
            var view = _pointerView ?? SceneView.lastActiveSceneView;
            var hud = EnsureHud(view);
            if (hud == null) return;
            hud.Hint.text = text;
            Place(hud.Hint, ToRoot(view, _pointerGui) + new Vector2(14f, 16f));
            Show(hud.Hint, true);
            _hintFollowsCursor = true;
            view.Repaint();
        }

        public static void HideHint()
        {
            _hintFollowsCursor = false;
            foreach (var hud in Huds.Values) Show(hud.Hint, false);
        }

        /// <summary>
        /// Shift / Ctrl を押している間、カーソル脇に「足す (+) / 外す (−)」を出す。
        /// ツールの duringSceneGui から毎回呼ぶ (add = e.shift、remove = e.control || e.command)。
        /// </summary>
        public static void SetModifier(SceneView view, Vector2 gui, bool add, bool remove)
        {
            if (view == null || !Huds.TryGetValue(view, out var hud)) return;
            bool show = add || remove;
            Show(hud.Badge, show);
            if (!show) return;
            hud.BadgeText.text = remove ? "−" : "+";
            hud.Badge.tooltip = remove ? "外す" : "足す";
            Place(hud.Badge, ToRoot(view, gui) + new Vector2(12f, -20f));
        }

        // ─── 内部 ───────────────────────────────────────────────────

        private static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextPoll)
            {
                _nextPoll = now + 0.5;
                foreach (SceneView view in SceneView.sceneViews) EnsureHud(view);
                var dead = new List<SceneView>();
                foreach (var kv in Huds) if (kv.Key == null) dead.Add(kv.Key);
                foreach (var d in dead) Huds.Remove(d);
            }

            // 迷い検知: 開始から一定時間シーンで操作が無ければ、カーソル脇に次の操作を 1 行だけ出す
            if (LostHintText != null && DennokoSceneSession.Active && !DennokoSceneSession.Interacted &&
                !_hintFollowsCursor && _lostHintCount < LostHintMax &&
                now - DennokoSceneSession.StartTime > LostHintDelay &&
                EditorWindow.mouseOverWindow is SceneView)
            {
                _lostHintCount++;
                string text = LostHintText();
                if (!string.IsNullOrEmpty(text)) ShowHint(text);
            }
        }

        private static void OnSceneGui(SceneView view)
        {
            var e = Event.current;
            _pointerView = view;
            _pointerGui = e.mousePosition;
            if (_hintFollowsCursor && e.type == EventType.MouseMove && Huds.TryGetValue(view, out var hud))
                Place(hud.Hint, ToRoot(view, e.mousePosition) + new Vector2(14f, 16f));
        }

        private static Hud EnsureHud(SceneView view)
        {
            if (view == null) return null;
            if (Huds.TryGetValue(view, out var existing) && existing.Root.panel != null) return existing;

            var parent = view.rootVisualElement;
            parent.Q(RootName)?.RemoveFromHierarchy();   // ドメインリロード前の残りを外す (二重にしない)

            var hud = new Hud { Root = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore } };
            DennokoSceneUI.SetupRoot(hud.Root);
            hud.Root.AddToClassList("dennoko-hud");

            // トースト (ホバー中は消さないので picking を受ける)
            hud.Toast = new VisualElement();
            hud.Toast.AddToClassList("dennoko-toast");
            hud.ToastIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            hud.ToastIcon.AddToClassList("dennoko-icon");
            hud.Toast.Add(hud.ToastIcon);
            hud.ToastText = new Label();
            hud.ToastText.AddToClassList("dennoko-toast__text");
            hud.ToastText.RegisterCallback<ClickEvent>(e => ToastTextClicked?.Invoke(e.clickCount));
            hud.Toast.Add(hud.ToastText);
            hud.ToastAction = new Button();
            hud.ToastAction.AddToClassList("dennoko-button-secondary");
            hud.ToastAction.AddToClassList("dennoko-toast__action");
            hud.ToastAction.clicked += () =>
            {
                var action = _toastAction;
                _toastAction = null;
                Show(hud.Toast, false);
                action?.Invoke();
            };
            hud.Toast.Add(hud.ToastAction);
            hud.Toast.RegisterCallback<MouseEnterEvent>(_ => hud.ToastHide?.Pause());
            hud.Toast.RegisterCallback<MouseLeaveEvent>(_ => ScheduleToastHide(hud, 1500));
            hud.Root.Add(hud.Toast);
            Show(hud.Toast, false);

            hud.Hint = new Label { pickingMode = PickingMode.Ignore };
            hud.Hint.AddToClassList("dennoko-cursor-hint");
            hud.Root.Add(hud.Hint);
            Show(hud.Hint, false);

            hud.Badge = new VisualElement { pickingMode = PickingMode.Ignore };
            hud.Badge.AddToClassList("dennoko-mod-badge");
            hud.BadgeText = new Label { pickingMode = PickingMode.Ignore };
            hud.BadgeText.AddToClassList("dennoko-mod-badge__text");
            hud.Badge.Add(hud.BadgeText);
            hud.Root.Add(hud.Badge);
            Show(hud.Badge, false);

            parent.Add(hud.Root);
            Huds[view] = hud;
            return hud;
        }

        private static void ScheduleToastHide(Hud hud, long ms)
        {
            hud.ToastHide?.Pause();
            hud.ToastHide = hud.Toast.schedule.Execute(() => Show(hud.Toast, false)).StartingIn(ms);
        }

        /// <summary>
        /// SceneView の GUI 座標 → rootVisualElement の座標。
        /// rootVisualElement はシーン上部のツールバーの分だけ背が高いので、その差だけ下へずらす。
        /// </summary>
        private static Vector2 ToRoot(SceneView view, Vector2 gui)
        {
            float camHeight = view.camera != null ? view.camera.pixelHeight / EditorGUIUtility.pixelsPerPoint : view.position.height;
            float offset = Mathf.Max(0f, view.rootVisualElement.layout.height - camHeight);
            return gui + new Vector2(0f, offset);
        }

        private static void Place(VisualElement e, Vector2 p)
        {
            e.style.left = p.x;
            e.style.top = p.y;
        }

        private static void Show(VisualElement e, bool visible)
        {
            if (e != null) e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
