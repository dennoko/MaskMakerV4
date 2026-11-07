using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// S4 シーンフィードバック（skill blocks/sceneview.md）のツール側。本体は DennokoSceneFeedback。
    ///   トースト      : シーンから出した通知（MessageOrigin.Scene）。ウィンドウから出した通知はステータスバー（出口は 1 か所）
    ///   迷い検知ヒント : 作業を始めてからシーンで何も操作していない時に 1 行（各ヒント 3 回まで。Preferences で数え直せる）
    ///   修飾キーのバッジ: SceneInput が Shift / Ctrl に合わせて出す
    /// 作業していない時はシーンに何も出さない（開始はツールタブと Hierarchy の右クリックから）。
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneHud
    {
        // DennokoSceneFeedback.RootName と同じ名前（トーストの操作ボタンを、候補メニューの表示位置に使う）
        private const string FeedbackRootName = "UVMaskMaker-SceneFeedback";
        private const int MaxHintShown = 3;

        private static string _toastAsset;

        static SceneHud()
        {
            Notifier.Posted += OnMessage;
            DennokoSceneFeedback.LostHintText = LostHint;
            DennokoSceneFeedback.ToastTextClicked = PingToast;
        }

        private static void OnMessage(UserMessage m)
        {
            if (m.Origin != MessageOrigin.Scene || !SessionController.IsWorking) return;

            _toastAsset = m.AssetPath;
            System.Action action = null;
            if (m.Action != null)
            {
                var run = m.Action;
                var view = SessionController.Current.Interaction.PointerView ?? SceneView.lastActiveSceneView;
                action = () => run(ToastActionButton(view));
            }
            DennokoSceneFeedback.Toast(m.Text, IconFor(m.Level), m.Action != null ? m.ActionLabel : null, action, atCursor: true);
        }

        /// <summary>成功・警告・エラーはアイコンの色だけで区別する（背景を塗らない）。</summary>
        private static string IconFor(MessageLevel level) => level switch
        {
            MessageLevel.Success => DennokoIcons.Success,
            MessageLevel.Warning => DennokoIcons.Warning,
            MessageLevel.Error => Icons.Error,
            _ => DennokoIcons.Info,
        };

        /// <summary>押されたトーストの操作ボタン（押した直後に隠れるが、位置はまだ残っている）。</summary>
        private static VisualElement ToastActionButton(SceneView view)
            => view != null ? view.rootVisualElement.Q(FeedbackRootName)?.Q(className: "dennoko-toast__action") : null;

        private static void PingToast(int clickCount)
        {
            if (string.IsNullOrEmpty(_toastAsset)) return;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(_toastAsset);
            if (obj == null) return;
            if (clickCount >= 2) EditorUtility.RevealInFinder(_toastAsset);
            else EditorGUIUtility.PingObject(obj);
        }

        /// <summary>迷っていそうな時の 1 行（選択モードだけ）。同じヒントは通算 3 回まで。</summary>
        private static string LostHint()
        {
            if (!SessionController.IsWorking || UiRules.PaintMode || SessionCommands.IsPaused) return null;
            var key = SettingKeys.HintShownCount("click");
            int n = SettingsService.Get(key);
            if (n >= MaxHintShown) return null;
            SettingsService.Set(key, n + 1);
            return L10n.T("hint.click");
        }
    }
}
