using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>SceneView のカメラ情報（ペイントの向き判定と図形の投影に使う）。</summary>
    internal struct ViewInfo
    {
        public Vector3 CameraPosition;
        public Vector3 CameraForward;
        public bool Orthographic;
        public Matrix4x4 ViewProjection;   // GPU 補正なし（ワールド → NDC）
        public Vector2 PixelSize;

        public static ViewInfo From(Camera cam)
        {
            return new ViewInfo
            {
                CameraPosition = cam.transform.position,
                CameraForward = cam.transform.forward,
                Orthographic = cam.orthographic,
                ViewProjection = cam.projectionMatrix * cam.worldToCameraMatrix,
                PixelSize = new Vector2(cam.pixelWidth, cam.pixelHeight),
            };
        }
    }

    /// <summary>対称の設定（Impl mask_processing §6.6）。</summary>
    internal struct MirrorInfo
    {
        public bool Enabled;
        public Matrix4x4 Matrix;    // ワールド → 基準空間で X 反転 → ワールド
    }

    /// <summary>手塗りレイヤーへの描画（Design gpu_pipeline §5.1）。</summary>
    internal sealed class PaintOps
    {
        private readonly GpuResources _gpu;
        private readonly TriMesh _tri;
        private readonly PaintSurface _surface;
        private readonly bool _wrap;

        public PaintOps(GpuResources gpu, TriMesh tri, PaintSurface surface, bool needsWrap)
        {
            _gpu = gpu;
            _tri = tri;
            _surface = surface;
            _wrap = needsWrap;
        }

        public void WorldCapsule(Vector3 a, Vector3 b, float radius, bool value, ViewInfo view, bool xray, MirrorInfo mirror, Matrix4x4 objectToWorld)
        {
            var m = _gpu.UVRaster;
            SetCommon(m, value, view, xray, mirror);
            m.SetFloat(ShaderIds.PaintMode, 0f);
            m.SetVector(ShaderIds.SegA, a);
            m.SetVector(ShaderIds.SegB, b);
            m.SetFloat(ShaderIds.Radius, radius);
            DrawWorld(m, objectToWorld);
        }

        public void WorldShape(Texture2D shape, bool value, ViewInfo view, bool xray, MirrorInfo mirror, Matrix4x4 objectToWorld)
        {
            var m = _gpu.UVRaster;
            SetCommon(m, value, view, xray, mirror);
            m.SetFloat(ShaderIds.PaintMode, 1f);
            m.SetMatrix(ShaderIds.ShapeVP, view.ViewProjection);
            m.SetTexture(ShaderIds.ShapeTex, shape);
            DrawWorld(m, objectToWorld);
        }

        private void SetCommon(Material m, bool value, ViewInfo view, bool xray, MirrorInfo mirror)
        {
            _tri.Bind(m);
            m.SetFloat(ShaderIds.PaintValue, value ? 1f : 0f);
            m.SetVector(ShaderIds.CamPos, view.CameraPosition);
            m.SetVector(ShaderIds.CamFwd, view.CameraForward);
            m.SetFloat(ShaderIds.CamOrtho, view.Orthographic ? 1f : 0f);
            m.SetFloat(ShaderIds.XRay, xray ? 1f : 0f);
            m.SetFloat(ShaderIds.Mirror, mirror.Enabled ? 1f : 0f);
            m.SetMatrix(ShaderIds.MirrorMatrix, mirror.Enabled ? mirror.Matrix : Matrix4x4.identity);
        }

        private void DrawWorld(Material m, Matrix4x4 objectToWorld)
        {
            using (new RenderTargetScope(_surface.Texture))
            {
                foreach (var offset in TriMesh.WrapOffsets(_wrap))
                {
                    m.SetVector(ShaderIds.UVOffset, offset);
                    m.SetPass(ShaderIds.RasterPaintWorld);
                    Graphics.DrawMeshNow(_tri.Mesh, objectToWorld);
                }
            }
            _surface.MarkPainted();
        }

        /// <summary>UV ビュー上のカプセル。a, b, radius は UV ビューの画面座標（px）。</summary>
        public void ViewCapsule(Vector2 a, Vector2 b, float radius, bool value, Vector4 uvToView)
        {
            var m = _gpu.ImageOps;
            m.SetFloat(ShaderIds.PaintMode, 0f);
            m.SetVector(ShaderIds.Seg2, new Vector4(a.x, a.y, b.x, b.y));
            m.SetFloat(ShaderIds.Radius, radius);
            BlitView(m, value, uvToView);
        }

        public void ViewShape(Texture2D shape, Vector2 viewSize, bool value, Vector4 uvToView)
        {
            var m = _gpu.ImageOps;
            m.SetFloat(ShaderIds.PaintMode, 1f);
            m.SetTexture(ShaderIds.ShapeTex, shape);
            m.SetVector(ShaderIds.ViewSize, new Vector4(viewSize.x, viewSize.y, 0, 0));
            BlitView(m, value, uvToView);
        }

        private void BlitView(Material m, bool value, Vector4 uvToView)
        {
            m.SetFloat(ShaderIds.PaintValue, value ? 1f : 0f);
            m.SetVector(ShaderIds.UVToView, uvToView);
            var prev = RenderTexture.active;
            Graphics.Blit(_gpu.Dummy, _surface.Texture, m, ShaderIds.OpPaintView);
            RenderTexture.active = prev;
            _surface.MarkPainted();
        }

        /// <summary>2 値画像（R8、0 / 255）の黒い部分を OR する。</summary>
        public void ImportOr(Texture2D binary)
        {
            var m = _gpu.ImageOps;
            m.SetTexture(ShaderIds.SrcTex, binary);
            var prev = RenderTexture.active;
            Graphics.Blit(_gpu.Dummy, _surface.Texture, m, ShaderIds.OpImportOr);
            RenderTexture.active = prev;
            _surface.MarkPainted();
        }
    }

    /// <summary>画面サイズの図形マスク（矩形・投げ縄）。</summary>
    internal sealed class ShapeMask
    {
        private readonly GpuResources _gpu;
        private Texture2D _tex;
        private byte[] _data;

        public ShapeMask(GpuResources gpu)
        {
            _gpu = gpu;
        }

        /// <summary>多角形を塗ったテクスチャ。何も塗られなければ null。</summary>
        public Texture2D Build(IReadOnlyList<Vector2> polygon, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (_tex == null || _tex.width != width || _tex.height != height)
            {
                _gpu.Release(_tex);
                _tex = _gpu.CreateTexture("ShapeMask", width, height, TextureFormat.R8, true);
                _data = new byte[width * height];
            }
            if (!PolygonFill.Fill(polygon, width, height, _data)) return null;
            _tex.LoadRawTextureData(_data);
            _tex.Apply(false, false);
            return _tex;
        }
    }
}
