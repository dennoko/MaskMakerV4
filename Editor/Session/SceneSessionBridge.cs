using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// SessionController ⇄ dennokoworks 共通の編集セッション（DennokoSceneSession。skill blocks/sceneview.md §4）。
    /// 作業の中身・保存・リロード後の再開は SessionController / SessionLifecycle が持ち、ここでは共通の約束だけをつなぐ:
    ///   同時に 1 つ（別の dennokoworks ツールが始めたら、こちらを終える）、Esc で終了、終了の理由の通知（DennokoSceneSession.Ended）。
    /// ショートカットのタグは「作業中かつ作業ツールが選ばれている間」だけ有効にするため SessionLifecycle が持ち、
    /// Unity のハンドルは作業ツール（MaskMakerTool）が選ばれている間は出ないので、ここでは隠さない。
    /// </summary>
    internal static class SceneSessionBridge
    {
        private const string DisplayName = "MaskMaker V4";

        /// <summary>共通の編集セッションがこのツールのものか。</summary>
        public static bool Active => DennokoSceneSession.Active && DennokoSceneSession.ToolId == SessionController.ToolId;

        /// <summary>作業を始めた。対象の切替・やり直しではセッションを続け、対象だけを差し替える。</summary>
        public static void OnStarted(Renderer target)
        {
            if (Active)
            {
                DennokoSceneSession.SwitchTarget(target.gameObject);
                return;
            }
            DennokoSceneSession.Begin(new DennokoSceneSession.Options
            {
                ToolId = SessionController.ToolId,
                DisplayName = DisplayName,
                Target = target.gameObject,
                ShortcutTag = null,
                OnEnd = OnSceneSessionEnd,
                HandleEscape = () => false,   // 選択が作業そのものなので、Esc はいつも終了
                IsInputBlocked = () => MaskMakerRadialMenu.Menu.IsOpen,   // ラジアルが開いている間の Esc はラジアルを閉じる
                HideUnityHandles = false,
            });
        }

        /// <summary>開始に失敗した（切替の途中なら前の作業は終わっているので、セッションも閉じる）。</summary>
        public static void OnStartFailed()
        {
            if (Active) DennokoSceneSession.End(DennokoSessionEndReason.User);
        }

        /// <summary>SessionController から作業を終えた。</summary>
        public static void OnEnded(DennokoSessionEndReason reason)
        {
            if (Active) DennokoSceneSession.End(reason);
        }

        /// <summary>共通の編集セッション側から終わった（Esc・別のツールの開始・Play モード・再コンパイル・シーン切替・対象の削除）。</summary>
        private static void OnSceneSessionEnd(DennokoSessionEndReason reason)
        {
            MaskMakerRadialMenu.Menu.Close(false);
            if (SessionController.Current == null) return;   // SessionController から終えた（もう後始末は済んでいる）
            SessionLifecycle.EndFromSceneSession(reason);
        }
    }
}
