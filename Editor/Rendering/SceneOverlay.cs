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
                line.SetColor(ShaderIds.Color, it.HoverWouldAdd ? Color.white : new Color(1f, 0.32f, 0.32f, 1f));
                line.SetFloat(ShaderIds.Thickness, 2f * EditorGUIUtility.pixelsPerPoint);
                line.SetPass(ShaderIds.LineWorld);
                Graphics.DrawMeshNow(s.HoverLines3D.Mesh, l2w);
            }

            if (!paused && paintMode) DrawPaintCursor(view, s);
            if (!paused && !paintMode) DrawModifierBadge(e);
        }

        private static void DrawPaintCursor(SceneView view, WorkSession s)
        {
            var it = s.Interaction;
            var tool = SettingsService.Get(SettingKeys.PaintTool);
            bool erase = PaintCommit.PaintValue(tool, Event.current.modifiers) == false;
            Handles.color = erase ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white;

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
                Handles.DrawAAPolyLine(2f, poly);
                Handles.EndGUI();
                return;
            }

            if (it.HoverValid)
            {
                float r = SceneInput.BrushWorldRadius(view.camera, it.HoverPoint);
                Handles.DrawWireDisc(it.HoverPoint, it.HoverNormal, r, 1.5f);
            }
            else
            {
                Handles.BeginGUI();
                Handles.DrawWireDisc(it.PointerGui, Vector3.forward, SettingsService.Get(SettingKeys.BrushSize));
                Handles.EndGUI();
            }
        }

        /// <summary>Shift / Ctrl を押している間、カーソル脇に + / - のアイコン（UI 設計 §2.6.3 段階 2）。</summary>
        private static void DrawModifierBadge(Event e)
        {
            string icon = e.shift ? Icons.Plus : e.control ? Icons.Minus : null;
            if (icon == null) return;
            var tex = Icons.Get(icon);
            if (tex == null) return;
            Handles.BeginGUI();
            GUI.DrawTexture(new Rect(e.mousePosition + new Vector2(14, 10), new Vector2(16, 16)), tex);
            Handles.EndGUI();
        }
    }
}
