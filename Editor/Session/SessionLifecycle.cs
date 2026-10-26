using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// Unity のイベント → SessionController（Design state_and_lifecycle §5.1）。
    /// static コンストラクタで 1 回だけ購読する（リロードで static は消え、もう一度走るので二重にならない）。
    /// </summary>
    [InitializeOnLoad]
    internal static class SessionLifecycle
    {
        private const string ResumeKey = "UVMaskMaker.ResumeTarget";
        public const string ShortcutTag = "UVMaskMaker.Working";

        private static bool _tagRegistered;

        static SessionLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneClosing += (scene, removing) => EndWithoutResume();
            PrefabStage.prefabStageOpened += _ => EndWithoutResume();
            PrefabStage.prefabStageClosing += _ => EndWithoutResume();
            EditorApplication.quitting += OnQuitting;
            EditorApplication.update += Update;
            EditorApplication.focusChanged += focused => { if (focused) SettingsService.ReloadProjectIfChanged(); };
            SessionController.Changed += UpdateShortcutTag;
            ToolManager.activeToolChanged += UpdateShortcutTag;
            EditorApplication.delayCall += AfterLoad;
        }

        private static void BeforeReload()
        {
            if (SessionController.Current != null)
                SessionState.SetString(ResumeKey, GlobalObjectId.GetGlobalObjectIdSlow(SessionController.Current.Target).ToString());
            SaveScheduler.FlushAll();
            SessionController.End();
            SetShortcutTag(false);
        }

        private static void AfterLoad()
        {
            GpuResources.SweepLeaks();
            TryResume();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                if (SessionController.Current != null)
                    SessionState.SetString(ResumeKey, GlobalObjectId.GetGlobalObjectIdSlow(SessionController.Current.Target).ToString());
                SaveScheduler.FlushAll();
                SessionController.End();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += TryResume;
            }
        }

        private static void EndWithoutResume()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.EraseString(ResumeKey);
            SessionController.End();
        }

        private static void OnQuitting()
        {
            SessionState.EraseString(ResumeKey);
            SaveScheduler.FlushAll();
            SessionController.End();
        }

        private static void TryResume()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string id = SessionState.GetString(ResumeKey, string.Empty);
            if (string.IsNullOrEmpty(id)) return;
            SessionState.EraseString(ResumeKey);
            if (!GlobalObjectId.TryParse(id, out var gid)) return;
            if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is Renderer r && r != null)
            {
                MaskMakerWindow.EnsureOpen();
                SessionController.Start(r, resume: true);
            }
        }

        private static void Update()
        {
            var s = SessionController.Current;
            if (s == null) return;
            if (s.Target == null)
            {
                EndWithoutResume();
                return;
            }
            s.Tick();
        }

        private static void UpdateShortcutTag()
        {
            SetShortcutTag(SessionController.Current != null && ToolManager.activeToolType == typeof(MaskMakerTool));
        }

        private static void SetShortcutTag(bool on)
        {
            if (on == _tagRegistered) return;
            _tagRegistered = on;
            if (on) ShortcutManager.RegisterTag(ShortcutTag);
            else ShortcutManager.UnregisterTag(ShortcutTag);
        }
    }
}
