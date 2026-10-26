using System;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 作業の開始・終了・再開の唯一の入口（Design state_and_lifecycle §5）。
    /// Current はリロードで消えるが、SessionLifecycle が再開トークンから作り直す。
    /// </summary>
    internal static class SessionController
    {
        public static WorkSession Current { get; private set; }
        public static bool IsWorking => Current != null;

        /// <summary>開始・終了・対象切替のたびに呼ばれる。</summary>
        public static event Action Changed;

        /// <summary>
        /// 作業を始める。resume = リロード / Play モード後の自動再開（Session 層を保ち、カメラを動かさない）。
        /// keepSession = 対象の切替（Session 層を保つ）。
        /// </summary>
        public static bool Start(Renderer target, bool resume = false, bool keepSession = false, MessageOrigin origin = MessageOrigin.Window)
        {
            if (target == null) return false;
            End();

            var tx = new StartTransaction();
            WorkSession session = null;
            try
            {
                string key = TargetKey.For(target);
                SettingsService.LoadTarget(key);
                tx.OnRollback(SettingsService.UnloadTarget);
                if (!resume && !keepSession) SettingsService.ClearSession();

                if (!WorkMeshSources.TryCreate(target, out var mesh, out string meshError))
                    throw new InvalidOperationException(meshError);
                session = new WorkSession(target, key, mesh);
                var created = session;
                tx.OnRollback(created.Dispose);

                session.Analyze(out string uvWarning);
                string restoreInfo = RestoreEditState(session);
                session.CreateGpu();

                if (SettingsService.Get(SettingKeys.IsolateTargetMesh)) session.Isolation.Apply(target.gameObject, true);
                tx.OnRollback(created.Isolation.Exit);

                ActivateTool();
                tx.Commit();

                Current = session;
                if (!resume && !keepSession)
                {
                    SettingsService.Set(SettingKeys.SymmetryEnabled, session.Analysis.MirrorMatchRate >= 0.9f);
                    FrameTarget(target);
                    Hints.OnSessionStarted();
                }

                Changed?.Invoke();
                SceneView.RepaintAll();

                if (uvWarning != null) Notifier.Post(MessageLevel.Warning, uvWarning, origin);
                else if (mesh.Info != null) Notifier.Post(MessageLevel.Info, mesh.Info, origin);
                else if (restoreInfo != null && !resume) Notifier.Post(MessageLevel.Info, restoreInfo, origin);
                return true;
            }
            catch (Exception e)
            {
                tx.Rollback();
                Current = null;
                Debug.LogException(e);
                Notifier.Post(MessageLevel.Error, L10n.F("error.startFailed", e.Message), origin);
                Changed?.Invoke();
                return false;
            }
        }

        /// <summary>メモリ上の作業が同じ対象ならそのまま使い（Undo 履歴も保つ）、違えばディスクから読む。</summary>
        private static string RestoreEditState(WorkSession session)
        {
            var es = EditState.instance;
            var a = session.Analysis;
            if (es.TargetKey == session.TargetKey && es.MeshHash == a.MeshHash && es.TriangleCount == a.TriangleCount
                && es.SelectedBits.Length == TriangleBits.WordCount(a.TriangleCount))
                return null;

            SessionPersistence.FlushPending();
            EditCommands.ResetForTarget(session.TargetKey, a.MeshHash, a.TriangleCount);
            if (!SessionFile.TryRead(StoragePaths.SessionFile(session.TargetKey), out var data)) return null;
            if (data.MeshHash != a.MeshHash || data.TriangleCount != a.TriangleCount
                || data.SelectedBits.Length != TriangleBits.WordCount(a.TriangleCount))
                return L10n.T("status.meshChanged");
            if (!TriangleBits.AnySet(data.SelectedBits) && PaintRle.IsEmpty(data.PaintRle)) return null;
            EditCommands.Restore(L10n.T("undo.restore"), data.SelectedBits, data.PaintRle);
            return L10n.T("status.restored");
        }

        /// <summary>作業を終える。何度呼んでもよい。</summary>
        public static void End(bool save = true)
        {
            var s = Current;
            if (s == null) return;
            Current = null;
            try
            {
                s.Paint?.DiscardStroke();
                if (save) SessionPersistence.FlushPending();
                SettingsService.FlushTarget();
                RestoreTool();
            }
            finally
            {
                s.Dispose();
                SettingsService.UnloadTarget();
                Changed?.Invoke();
                SceneView.RepaintAll();
            }
        }

        /// <summary>同じアバター内の別の Renderer へ切り替える（現在の作業は保存、切替先の前回作業を復元）。</summary>
        public static void SwitchTarget(Renderer target)
        {
            if (target == null || (Current != null && Current.Target == target)) return;
            Start(target, keepSession: true);
        }

        /// <summary>UV チャンネルの変更など、解析をやり直す時（選択と手塗りは EditState にあるので保たれる）。</summary>
        public static void Restart()
        {
            var s = Current;
            if (s == null) return;
            Start(s.Target, resume: true);
        }

        private static void ActivateTool()
        {
            if (ToolManager.activeToolType != typeof(MaskMakerTool)) ToolManager.SetActiveTool<MaskMakerTool>();
        }

        private static void RestoreTool()
        {
            if (ToolManager.activeToolType == typeof(MaskMakerTool)) ToolManager.RestorePreviousPersistentTool();
        }

        private static void FrameTarget(Renderer target)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.Frame(target.bounds, false);
        }
    }

    /// <summary>作業状態の自動保存（Impl mask_processing §3.5）。</summary>
    [InitializeOnLoad]
    internal static class SessionPersistence
    {
        private const string JobId = "session";

        static SessionPersistence()
        {
            EditState.Changed += _ => SaveScheduler.Schedule(JobId, 2.0, SaveNow);
        }

        public static void FlushPending() => SaveScheduler.Flush(JobId);

        public static void SaveNow()
        {
            var es = EditState.instance;
            if (string.IsNullOrEmpty(es.TargetKey)) return;
            try
            {
                SessionFile.Write(StoragePaths.SessionFile(es.TargetKey), new SessionFile.Data
                {
                    MeshHash = es.MeshHash,
                    TriangleCount = es.TriangleCount,
                    SelectedBits = es.SelectedBits,
                    PaintRle = es.PaintRle,
                });
            }
            catch (System.IO.IOException e)
            {
                Debug.LogWarning("[MaskMaker V4] " + e.Message);
            }
        }
    }
}
