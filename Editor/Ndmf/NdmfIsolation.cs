using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Dennoko.UVMaskMaker.Ndmf
{
    /// <summary>
    /// 孤立表示で NDMF のプレビュー用シーン（プロキシ）まで隠れ、対象が描画されなくなるのを防ぐ（Impl work_context_and_rendering §2.3）。
    /// プレビュー中の対象は元の Renderer の代わりにプロキシが描画されるため、プロキシが隠れると何も見えなくなる。
    /// プレビュー用シーンを表示に戻しても、元が非表示のプロキシは NDMF 自身がシーンビューで描画しないので、見えるのは対象だけ。
    /// 孤立表示中の変更なので、終了時の ExitIsolation で元に戻る。
    /// </summary>
    [InitializeOnLoad]
    internal static class NdmfIsolation
    {
        static NdmfIsolation()
        {
            Isolation.Applied += ShowPreviewScenes;
            // 孤立表示中にプレビュー用シーンが開き直された時（開いた直後はまだ NDMF に登録されていないので次のフレームで）
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                if (IsolatedByUs) EditorApplication.delayCall += () => { if (IsolatedByUs) ShowPreviewScenes(); };
            };
        }

        private static bool IsolatedByUs => SessionController.Current?.Isolation.Active == true;

        private static void ShowPreviewScenes()
        {
            var svm = SceneVisibilityManager.instance;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !NDMFPreviewSceneManager.IsPreviewScene(scene)) continue;
                if (svm.AreAnyDescendantsHidden(scene)) svm.Show(scene);
            }
        }
    }
}
