using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 選択の入力（スマートトグル・なぞり・ダブルクリック。Impl selection_system §4）。
    /// SceneView と UV ビューで共用する。
    /// </summary>
    internal sealed class SelectGesture
    {
        private readonly HashSet<int> _visited = new HashSet<int>();
        private bool _active;
        private bool _add;
        private int _undoGroup;
        private int _lastClickGroup = -1;

        public bool Active => _active;

        /// <summary>
        /// groups = 押した位置のグループ（現在の粒度）、triangles = 押した位置の三角形（ダブルクリックでパーツを引く）。
        /// </summary>
        public void Begin(WorkSession s, IReadOnlyList<int> groups, IReadOnlyList<int> triangles, EventModifiers mods, int clickCount)
        {
            if (clickCount >= 2 && _lastClickGroup >= 0)
            {
                // 1 回目のクリックを取り消し（Redo にも残さない）、パーツ一括選択を 1 件として積む
                Undo.RevertAllDownToGroup(_lastClickGroup);
                EditState.instance.ResyncAfterExternalChange();
                _lastClickGroup = -1;
                DoubleClick(s, triangles, mods);
                return;
            }

            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(L10n.T("undo.select"));
            _lastClickGroup = _undoGroup;

            _add = DecideAdd(s, s.Groups, groups, mods);
            _visited.Clear();
            _active = true;
            Apply(s, groups);
        }

        public void Move(WorkSession s, IReadOnlyList<int> groups)
        {
            if (!_active) return;
            var fresh = new List<int>();
            foreach (int g in groups)
                if (_visited.Add(g)) fresh.Add(g);
            if (fresh.Count > 0) SessionCommands.ApplyGroups(fresh, _add, s.Groups, true);
        }

        public void End()
        {
            if (!_active) return;
            _active = false;
            Undo.CollapseUndoOperations(_undoGroup);
        }

        public void Cancel() => End();

        private void Apply(WorkSession s, IReadOnlyList<int> groups)
        {
            foreach (int g in groups) _visited.Add(g);
            SessionCommands.ApplyGroups(groups, _add, s.Groups, true);
        }

        private static void DoubleClick(WorkSession s, IReadOnlyList<int> triangles, EventModifiers mods)
        {
            var parts = s.Analysis.Parts;
            var groups = new List<int>();
            foreach (int t in triangles)
            {
                int g = parts.TriToGroup[t];
                if (!groups.Contains(g)) groups.Add(g);
            }
            if (groups.Count == 0) return;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(L10n.T("undo.select"));
            SessionCommands.ApplyGroups(groups, DecideAdd(s, parts, groups, mods), parts, true);
        }

        /// <summary>Shift = 強制追加、Ctrl = 強制除外、それ以外はスマートトグル。</summary>
        public static bool DecideAdd(WorkSession s, GroupTable table, IReadOnlyList<int> groups, EventModifiers mods)
        {
            if ((mods & EventModifiers.Shift) != 0) return true;
            if ((mods & EventModifiers.Control) != 0) return false;
            return SelectionOps.ToggleResult(EditState.instance.SelectedBits, table, groups);
        }
    }

    /// <summary>ストロークの確定（読み戻し → 圧縮 → Undo に 1 件）。</summary>
    internal static class PaintCommit
    {
        public static void Commit(WorkSession s, string undoName = null)
        {
            if (s == null || !s.Paint.StrokeDirty) return;
            byte[] rle = s.Paint.ReadAndEncode();
            EditCommands.CommitPaint(undoName ?? L10n.T("undo.paint"), rle);
            s.Paint.AcceptCommitted(EditState.instance.PaintRevision);
        }

        /// <summary>Shift = 塗り、Ctrl = 消去、それ以外はツールに従う（消しゴムは消去）。</summary>
        public static bool PaintValue(PaintSubMode tool, EventModifiers mods)
        {
            if ((mods & EventModifiers.Shift) != 0) return true;
            if ((mods & EventModifiers.Control) != 0) return false;
            return tool != PaintSubMode.Eraser;
        }
    }

    /// <summary>SceneView のペイント入力（Impl mask_processing §6.3–6.6）。</summary>
    internal sealed class ScenePaintGesture
    {
        private bool _active;
        private bool _value;
        private PaintSubMode _tool;
        private bool _hasPrev;
        private Vector3 _prev;

        public bool Active => _active;

        public void Begin(SceneView view, WorkSession s, Event e)
        {
            s.EnsureGpu();
            _active = true;
            _tool = SettingsService.Get(SettingKeys.PaintTool);
            _value = PaintCommit.PaintValue(_tool, e.modifiers);
            _hasPrev = false;
            s.Interaction.Stroking = true;
            s.Interaction.ShapePoints.Clear();
            s.Interaction.ShapeInUVView = false;
            if (IsShape) s.Interaction.ShapePoints.Add(e.mousePosition);
            else Stamp(view, s);
        }

        public void Move(SceneView view, WorkSession s, Event e)
        {
            if (!_active) return;
            if (!IsShape)
            {
                Stamp(view, s);
                return;
            }
            var pts = s.Interaction.ShapePoints;
            if (_tool == PaintSubMode.Rect)
            {
                if (pts.Count < 2) pts.Add(e.mousePosition);
                else pts[1] = e.mousePosition;
            }
            else if (pts.Count == 0 || (pts[pts.Count - 1] - e.mousePosition).sqrMagnitude >= 4f)
            {
                pts.Add(e.mousePosition);
            }
        }

        public void End(SceneView view, WorkSession s)
        {
            if (!_active) return;
            _active = false;
            s.Interaction.Stroking = false;
            if (IsShape) ApplyShape(view, s);
            s.Interaction.ShapePoints.Clear();
            PaintCommit.Commit(s);
        }

        private bool IsShape => _tool == PaintSubMode.Rect || _tool == PaintSubMode.Lasso;

        private void Stamp(SceneView view, WorkSession s)
        {
            var it = s.Interaction;
            if (!it.HoverValid)
            {
                _hasPrev = false; // メッシュから外れた区間は結ばない
                return;
            }
            Vector3 a = _hasPrev ? _prev : it.HoverPoint;
            float r = SceneInput.BrushWorldRadius(view.camera, it.HoverPoint);
            s.PaintOps.WorldCapsule(a, it.HoverPoint, r, _value, ViewInfo.From(view.camera),
                SettingsService.Get(SettingKeys.OverlayOnTop), s.Mirror, s.Mesh.LocalToWorld);
            _prev = it.HoverPoint;
            _hasPrev = true;
        }

        private void ApplyShape(SceneView view, WorkSession s)
        {
            var gui = s.Interaction.ShapePoints;
            List<Vector2> poly;
            if (_tool == PaintSubMode.Rect)
            {
                if (gui.Count < 2) return;
                poly = PolygonFill.Rect(gui[0], gui[1]);
            }
            else
            {
                if (gui.Count < 3) return;
                poly = new List<Vector2>(gui);
            }
            for (int i = 0; i < poly.Count; i++) poly[i] = HandleUtility.GUIPointToScreenPixelCoordinate(poly[i]);
            var cam = view.camera;
            var tex = s.ShapeMask.Build(poly, cam.pixelWidth, cam.pixelHeight);
            if (tex == null) return;
            s.PaintOps.WorldShape(tex, _value, ViewInfo.From(cam), SettingsService.Get(SettingKeys.OverlayOnTop), s.Mirror, s.Mesh.LocalToWorld);
        }
    }
}
