using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// UV ビューの描画（Impl work_context_and_rendering §3.4）。表示は SceneView と同じ D。
    /// RT に描いて UI Toolkit の Image で見せる。
    /// </summary>
    internal sealed class UVViewRenderer
    {
        private readonly GpuResources _gpu;
        private readonly Mesh _quad;
        private RenderTexture _rt;

        public UVViewRenderer(GpuResources gpu)
        {
            _gpu = gpu;
            _quad = gpu.CreateMesh("UVQuad");
            _quad.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.uv2 = new Vector2[4];
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }

        public RenderTexture Target => _rt;

        /// <summary>
        /// uvToView: view(px, y 下向き) = uv * xy + zw。size: 表示サイズ（px）。
        /// </summary>
        public RenderTexture Render(WorkSession s, Vector2Int size, Vector4 uvToView, bool hover, bool hoverAdd)
        {
            size.x = Mathf.Max(8, size.x);
            size.y = Mathf.Max(8, size.y);
            if (_rt == null || _rt.width != size.x || _rt.height != size.y || !_rt.IsCreated())
            {
                _gpu.Release(_rt);
                _rt = _gpu.CreateColorRT("UVView", size.x, size.y);
            }

            // view(px, y 下向き) → NDC(y 上向き): ndc = uv * scale + offset
            var toClip = new Vector4(
                2f * uvToView.x / size.x,
                -2f * uvToView.y / size.y,
                2f * uvToView.z / size.x - 1f,
                1f - 2f * uvToView.w / size.y);

            using (new RenderTargetScope(_rt))
            {
                GL.Clear(false, true, new Color(0.07f, 0.07f, 0.07f, 1f));

                var o = _gpu.Overlay;
                o.SetVector(ShaderIds.UVToClip, toClip);
                o.SetTexture(ShaderIds.MaskTex, s.Pipeline.Mask);
                var baseTex = SettingsService.Get(SettingKeys.PreviewOverlayBaseTex) ? s.BaseTexture : null;
                o.SetTexture(ShaderIds.BaseTex, baseTex != null ? (Texture)baseTex : Texture2D.blackTexture);
                o.SetFloat(ShaderIds.BaseAlpha, baseTex != null ? SettingsService.Get(SettingKeys.PreviewOverlayAlpha) : 0f);
                o.SetColor(ShaderIds.BgColor, new Color(0.118f, 0.118f, 0.118f, 1f));
                var c = SettingsService.Get(SettingKeys.SelectedSceneColor);
                c.a = SettingsService.Get(SettingKeys.SceneMaskAlpha);
                o.SetColor(ShaderIds.Color, c);
                o.SetPass(ShaderIds.OverlayUVView);
                Graphics.DrawMeshNow(_quad, Matrix4x4.identity);

                var line = _gpu.Line;
                line.SetVector(ShaderIds.UVToClip, toClip);
                line.SetVector(ShaderIds.TargetSize, new Vector4(size.x, size.y, 0, 0));
                DrawLines(line, s.WireLinesUV.Mesh, new Color(1f, 1f, 1f, 0.18f), 1f);
                DrawLines(line, s.BorderLinesUV.Mesh, SettingsService.Get(SettingKeys.SeamColor), 1.5f);
                if (hover && s.HoverLinesUV.SegmentCount > 0)
                    DrawLines(line, s.HoverLinesUV.Mesh, hoverAdd ? Color.white : new Color(1f, 0.32f, 0.32f, 1f), 2f);
            }
            return _rt;
        }

        private static void DrawLines(Material line, Mesh mesh, Color color, float thickness)
        {
            if (mesh.vertexCount == 0) return;
            line.SetColor(ShaderIds.Color, color);
            line.SetFloat(ShaderIds.Thickness, thickness);
            line.SetPass(ShaderIds.LineUV);
            Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
        }
    }
}
