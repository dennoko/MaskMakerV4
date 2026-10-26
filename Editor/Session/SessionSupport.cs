using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>作業開始の途中で失敗した時、登録した逆順で戻す（Design state_and_lifecycle §5.2）。</summary>
    internal sealed class StartTransaction
    {
        private readonly List<Action> _rollback = new List<Action>();
        private bool _committed;

        public void OnRollback(Action action) => _rollback.Add(action);

        public void Commit()
        {
            _committed = true;
            _rollback.Clear();
        }

        public void Rollback()
        {
            if (_committed) return;
            for (int i = _rollback.Count - 1; i >= 0; i--)
            {
                try { _rollback[i](); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _rollback.Clear();
        }
    }

    /// <summary>孤立表示。自分が始めた時だけ解除する（Impl work_context_and_rendering §2.3）。</summary>
    internal sealed class Isolation
    {
        private bool _ours;

        public void Apply(GameObject target, bool on)
        {
            var svm = SceneVisibilityManager.instance;
            if (on)
            {
                if (_ours || svm.IsCurrentStageIsolated()) return;
                svm.Isolate(target, true);
                _ours = true;
            }
            else
            {
                Exit();
            }
        }

        public void Exit()
        {
            if (!_ours) return;
            _ours = false;
            SceneVisibilityManager.instance.ExitIsolation();
        }
    }

    /// <summary>ホバー・ドラッグ中などの一時状態（保存しない。Design state_and_lifecycle §1）。</summary>
    internal sealed class InteractionState
    {
        // SceneView のホバー
        public bool HoverValid;
        public int HoverTriangle = -1;
        public Vector3 HoverPoint;
        public Vector3 HoverNormal;
        public Vector2 PointerGui;            // SceneView の GUI 座標
        public SceneView PointerView;

        // ホバー中のグループ（UV ビューでは複数）
        public readonly List<int> HoverGroups = new List<int>();
        public int HoverMirrorGroup = -1;
        public bool HoverWouldAdd = true;
        public bool HoverFromUVView;

        // ブラシ・図形の描画中
        public bool Stroking;
        public readonly List<Vector2> ShapePoints = new List<Vector2>();
        public bool ShapeInUVView;

        // キーを押している間だけの状態
        public bool SpaceHeld;
        public bool RadialOpen;

        public void ClearHover()
        {
            HoverValid = false;
            HoverTriangle = -1;
            HoverGroups.Clear();
            HoverMirrorGroup = -1;
        }
    }
}
