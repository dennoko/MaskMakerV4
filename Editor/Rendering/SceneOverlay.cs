using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// SceneView への描画（Impl work_context_and_rendering §3）。一時停止中も描き続けるので EditorTool ではなく duringSceneGui で描く。
    /// GPU の更新（合成・転送）は Layout イベントで行い、Repaint では描くだけにする。
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneOverlay
    {
        static SceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var s = SessionController.Current;
            if (s == null) return;
            var e = Event.current;
            if (e.type == EventType.Layout)
            {
                try
                {
                    s.EnsureGpu();
                    s.EnsureHoverLines();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
                return;
            }
            if (e.type != EventType.Repaint || s.Pipeline.Mask == null) return;

            var cam = view.camera;
            bool xray = SettingsService.Get(SettingKeys.OverlayOnTop);
            float zTest = (float)(xray ? CompareFunction.Always : CompareFunction.LessEqual);
            var l2w = s.Mesh.LocalToWorld;

            // マスク
            var overlay = s.Gpu.Overlay;
            s.Tri.Bind(overlay);
            overlay.SetTexture(ShaderIds.MaskTex, s.Pipeline.Mask);
            var color = SettingsService.Get(SettingKeys.SelectedSceneColor);
            color.a = SettingsService.Get(SettingKeys.SceneMaskAlpha);
            overlay.SetColor(ShaderIds.Color, color);
            overlay.SetFloat(ShaderIds.ZTest, zTest);
            overlay.SetPass(ShaderIds.OverlayScene);
            Graphics.DrawMeshNow(s.Tri.Mesh, l2w);

            // シームとホバーの縁取り
            var line = s.Gpu.Line;
            line.SetVector(ShaderIds.TargetSize, new Vector4(cam.pixelWidth, cam.pixelHeight, 0, 0));
            line.SetFloat(ShaderIds.ZTest, zTest);
            line.SetColor(ShaderIds.Color, SettingsService.Get(SettingKeys.SeamColor));
            line.SetFloat(ShaderIds.Thickness, SettingsService.Get(SettingKeys.OverlaySeamThickness) * EditorGUIUtility.pixelsPerPoint);
            if (s.SeamLines.SegmentCount > 0)
            {
                line.SetPass(ShaderIds.LineWorld);
                Graphics.DrawMeshNow(s.SeamLines.Mesh, l2w);
            }

            bool paintMode = SettingsService.Get(SettingKeys.IsPaintMode);
            bool paused = SessionCommands.IsPaused;
            var it = s.Interaction;
            if (!paused && !paintMode && it.HoverGroups.Count > 0 && s.HoverLines3D.SegmentCount > 0)
            {
                line.SetColor(ShaderIds.Color, it.HoverWouldAdd ? DennokoSceneColors.HoverAdd : DennokoSceneColors.HoverRemove);
                line.SetFloat(ShaderIds.Thickness, 2f * EditorGUIUtility.pixelsPerPoint);
                line.SetPass(ShaderIds.LineWorld);
                Graphics.DrawMeshNow(s.HoverLines3D.Mesh, l2w);
            }

            if (!paused && paintMode) DrawPaintCursor(view, s);
        }

        private static void DrawPaintCursor(SceneView view, WorkSession s)
        {
            var it = s.Interaction;
            var tool = SettingsService.Get(SettingKeys.PaintTool);
            // 色と線は skill S5（DennokoSceneColors）。消す時は赤に加えて点線にする（色だけで意味を伝えない）
            bool erase = PaintCommit.PaintValue(tool, Event.current.modifiers) == false;
            var color = erase ? DennokoSceneColors.HoverRemove : DennokoSceneColors.BrushInner;

            if (tool == PaintSubMode.Rect || tool == PaintSubMode.Lasso)
            {
                var pts = it.ShapePoints;
                if (!it.Stroking || it.ShapeInUVView || pts.Count < 2) return;
                Handles.BeginGUI();
                Vector3[] poly;
                if (tool == PaintSubMode.Rect)
                {
                    Vector2 a = pts[0], b = pts[1];
                    poly = new Vector3[] { a, new Vector2(b.x, a.y), b, new Vector2(a.x, b.y), a };
                }
                else
                {
                    poly = new Vector3[pts.Count + 1];
                    for (int i = 0; i < pts.Count; i++) poly[i] = pts[i];
                    poly[pts.Count] = pts[0];
                }
                DennokoSceneColors.Polyline(poly, color, DennokoSceneColors.Bold, erase);
                Handles.EndGUI();
                return;
            }

            if (it.HoverValid)
            {
                float r = SceneInput.BrushWorldRadius(view.camera, it.HoverPoint);
                DennokoSceneColors.Circle(it.HoverPoint, it.HoverNormal, r, color, DennokoSceneColors.Bold, erase);
            }
            else
            {
                Handles.BeginGUI();
                DennokoSceneColors.Circle(it.PointerGui, Vector3.forward, SettingsService.Get(SettingKeys.BrushSize), color, DennokoSceneColors.Bold, erase);
                Handles.EndGUI();
            }
        }
    }
}
