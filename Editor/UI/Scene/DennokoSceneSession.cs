using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    internal enum DennokoSessionEndReason
    {
        User,          // ウィンドウ / Inspector の「終了」、パネルの ✕
        Escape,        // シーンで Esc
        PlayMode,      // Play モードに入る
        Reload,        // 再コンパイル (ドメインリロード) の前
        SceneChanged,  // シーンの切替
        TargetLost,    // 対象が削除された
        Replaced,      // 別のセッション (このツール / 別の dennokoworks ツール) が始まった
        WindowClosed,  // ツールのウィンドウを閉じた (ツール側が OnDisable から呼ぶ)
    }

    /// <summary>
    /// シーンの編集セッションの約束 (blocks/sceneview.md §4) を 1 か所で守る共通クラス。そのままコピーして使う。
    ///
    ///   開始 : ウィンドウ / Inspector の主ボタンから Begin。シーンに開始ボタンを出さない
    ///   終了 : Esc / パネルの ✕ / バナーの「終了」。どれも End を呼ぶ
    ///   自動終了 : Play モード・再コンパイル・シーン切替・対象の削除
    ///   同時に 1 つ : 別のセッションが始まったら Replaced で終わる (別の dennokoworks ツールのコピーとも SessionState で連絡する)
    ///   作業中だけ : ショートカットのタグを登録し、Unity の移動 / 回転ハンドルを隠す
    ///
    /// ツールの値 (半径・モードなど) はここに持たない。ツールの状態クラス (YourSceneToolState) に持ち、
    /// 表示の読み直しは状態クラスの Changed で行う。ここが出すのは「作業中かどうか」の合図 (ActiveChanged) だけ。
    /// </summary>
    [InitializeOnLoad]
    internal static class DennokoSceneSession
    {
        internal sealed class Options
        {
            /// <summary>ツールの識別名 (例: "DenMeshEditor")。</summary>
            public string ToolId;
            /// <summary>表示名 (例: "Mesh Editor")。終了メッセージなどに使う。</summary>
            public string DisplayName;
            /// <summary>編集する対象。null でもよい (対象が消えたときの自動終了をしないだけ)。</summary>
            public GameObject Target;
            /// <summary>表示する対象名。省略時は Target.name。</summary>
            public string TargetName;
            /// <summary>作業中だけ有効にするショートカットのタグ (例: "Dennokoworks.DenMeshEditor.Editing")。</summary>
            public string ShortcutTag;
            /// <summary>終了するときに呼ぶ。作業の保存と後始末をここで行う (どの終わり方でも必ず呼ばれる)。</summary>
            public Action<DennokoSessionEndReason> OnEnd;
            /// <summary>Esc を押したときに先に呼ぶ。true を返したら終了しない (例: 選択があれば解除して true)。</summary>
            public Func<bool> HandleEscape;
            /// <summary>true の間は Esc を見ない (例: ラジアルメニューが開いている間。Esc はメニューを閉じるのに使う)。</summary>
            public Func<bool> IsInputBlocked;
            /// <summary>作業中は Unity の移動 / 回転ハンドルを隠す (既定 true)。</summary>
            public bool HideUnityHandles = true;
        }

        private const string OwnerKey = "Dennokoworks.SceneSession.Owner";

        /// <summary>開始・終了・対象の切替のたびに呼ぶ。B7 バナーや S2 のセッション行はこれで読み直す。</summary>
        public static event Action ActiveChanged;
        /// <summary>終了した理由。ウィンドウはステータスバーに EndMessage を出す。</summary>
        public static event Action<DennokoSessionEndReason> Ended;

        private static Options _options;
        private static string _token;
        private static bool _hadTarget;
        private static bool _prevToolsHidden;
        private static double _nextOwnerCheck;

        public static bool Active => _options != null;
        public static string ToolId => _options?.ToolId;
        public static string DisplayName => _options?.DisplayName ?? string.Empty;
        public static GameObject Target { get; private set; }
        public static string TargetName { get; private set; } = string.Empty;
        public static double StartTime { get; private set; }
        /// <summary>開始後にシーンで操作したか (迷っていそうなときのヒント用)。</summary>
        public static bool Interacted { get; private set; }

        static DennokoSceneSession()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) End(DennokoSessionEndReason.PlayMode);
            };
            // static はドメインリロードで消えるので、タグ・ハンドルの後始末を先にしておく
            AssemblyReloadEvents.beforeAssemblyReload += () => End(DennokoSessionEndReason.Reload);
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => End(DennokoSessionEndReason.SceneChanged);
            EditorApplication.hierarchyChanged += () =>
            {
                if (Active && _hadTarget && Target == null) End(DennokoSessionEndReason.TargetLost);
            };
            EditorApplication.update += CheckOwner;
            SceneView.duringSceneGui += OnSceneGui;
        }

        // ─── 開始 / 終了 ───────────────────────────────────────────────

        public static void Begin(Options options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (Active) End(DennokoSessionEndReason.Replaced);   // 同時に 1 つ

            _options = options;
            _token = $"{options.ToolId}:{Guid.NewGuid():N}";
            SessionState.SetString(OwnerKey, _token);   // 他のツールのコピーはこれを見て自分を終える

            Target = options.Target;
            _hadTarget = Target != null;
            TargetName = options.TargetName ?? (Target != null ? Target.name : string.Empty);
            StartTime = EditorApplication.timeSinceStartup;
            Interacted = false;

            SetShortcutTag(options.ShortcutTag, true);
            if (options.HideUnityHandles)
            {
                _prevToolsHidden = Tools.hidden;
                Tools.hidden = true;
            }

            ActiveChanged?.Invoke();
            SceneView.RepaintAll();
        }

        public static void End(DennokoSessionEndReason reason)
        {
            if (!Active) return;
            var options = _options;
            _options = null;   // OnEnd の中から End が呼ばれても二重にしない

            if (SessionState.GetString(OwnerKey, string.Empty) == _token) SessionState.EraseString(OwnerKey);
            SetShortcutTag(options.ShortcutTag, false);
            if (options.HideUnityHandles) Tools.hidden = _prevToolsHidden;

            try { options.OnEnd?.Invoke(reason); }
            catch (Exception e) { Debug.LogException(e); }

            Ended?.Invoke(reason);
            ActiveChanged?.Invoke();
            SceneView.RepaintAll();
        }

        /// <summary>作業中に対象を切り替える (B1 / B7 の ▼ から。Hierarchy の選択では変えない)。</summary>
        public static void SwitchTarget(GameObject target, string targetName = null)
        {
            if (!Active) return;
            Target = target;
            _hadTarget = target != null;
            TargetName = targetName ?? (target != null ? target.name : string.Empty);
            ActiveChanged?.Invoke();
            SceneView.RepaintAll();
        }

        /// <summary>シーンで操作したことを知らせる (クリック・ドラッグのたびに呼ぶ。ヒントを止める)。</summary>
        public static void MarkInteracted()
        {
            if (Interacted) return;
            Interacted = true;
            DennokoSceneFeedback.HideHint();
        }

        /// <summary>SceneView を対象に合わせる (バナー / パネルの対象名クリック)。</summary>
        public static void FrameTarget()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || Target == null) return;
            var renderers = Target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                view.Frame(new Bounds(Target.transform.position, Vector3.one * 0.5f), false);
                return;
            }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            view.Frame(b, false);
        }

        /// <summary>終了の理由をステータスバー向けの 1 文にする (ツールの i18n に置き換えてよい)。</summary>
        public static string EndMessage(DennokoSessionEndReason reason) => reason switch
        {
            DennokoSessionEndReason.Escape       => "Esc で作業を終了しました",
            DennokoSessionEndReason.PlayMode     => "Play モードに入るため作業を終了しました",
            DennokoSessionEndReason.Reload       => "再コンパイルのため作業を終了しました",
            DennokoSessionEndReason.SceneChanged => "シーンが切り替わったため作業を終了しました",
            DennokoSessionEndReason.TargetLost   => "対象が削除されたため作業を終了しました",
            DennokoSessionEndReason.Replaced     => "別の作業を始めたため、こちらは終了しました",
            DennokoSessionEndReason.WindowClosed => "ウィンドウを閉じたため作業を終了しました",
            _                                    => "作業を終了しました",
        };

        // ─── 内部 ───────────────────────────────────────────────────

        // 別の dennokoworks ツール (別 namespace の DennokoSceneSession) が始めたら、こちらを終える
        private static void CheckOwner()
        {
            if (!Active) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextOwnerCheck) return;
            _nextOwnerCheck = now + 0.25;
            string owner = SessionState.GetString(OwnerKey, string.Empty);
            if (!string.IsNullOrEmpty(owner) && owner != _token) End(DennokoSessionEndReason.Replaced);
        }

        // Esc: まずツールに渡し (選択の解除など)、処理されなければ終了する
        private static void OnSceneGui(SceneView view)
        {
            if (!Active) return;
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return;
            if (_options.IsInputBlocked != null && _options.IsInputBlocked()) return;

            if (_options.HandleEscape != null && _options.HandleEscape())
            {
                e.Use();
                return;
            }
            e.Use();
            End(DennokoSessionEndReason.Escape);
        }

        private static void SetShortcutTag(string tag, bool on)
        {
            if (string.IsNullOrEmpty(tag)) return;
            if (on) ShortcutManager.RegisterTag(tag);
            else ShortcutManager.UnregisterTag(tag);
        }
    }
}
