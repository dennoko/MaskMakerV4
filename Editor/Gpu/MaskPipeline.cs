using System;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// Island / Coverage / PaintLow / 余白 / D の合成（Impl mask_processing §2、Design gpu_pipeline §4）。
    /// Ensure は入力のバージョンを比べ、変わった手順だけを実行する。表示と出力は常に Mask を読む。
    /// </summary>
    internal sealed class MaskPipeline : IDisposable
    {
        private readonly GpuResources _gpu;
        private readonly TriMesh _tri;
        private readonly PaintSurface _paint;

        private RenderTexture _coverage, _island, _merged, _temp, _mask;
        private int _width, _height;
        private int _coverageKey = -1, _islandKey = -1;
        private int _coverageVersion, _islandVersion;
        private (int, int, int, int) _compositeKey = (-1, -1, -1, -1);
        private bool _wrap;

        public RenderTexture Mask => _mask;
        public int Width => _width;
        public int Height => _height;
        /// <summary>D が作り直されるたびに +1（UV ビューの再描画判定に使う）。</summary>
        public int Version { get; private set; }

        public MaskPipeline(GpuResources gpu, TriMesh tri, PaintSurface paint, bool needsWrap)
        {
            _gpu = gpu;
            _tri = tri;
            _paint = paint;
            _wrap = needsWrap;
        }

        public void Ensure(int width, int height, int margin)
        {
            bool resized = EnsureTargets(width, height);
            if (resized || _coverageKey != _tri.ActiveVersion)
            {
                DrawUV(_coverage, ShaderIds.RasterCoverage);
                _coverageKey = _tri.ActiveVersion;
                _coverageVersion++;
            }
            if (resized || _islandKey != _tri.FlagsVersion)
            {
                DrawUV(_island, ShaderIds.RasterIsland);
                _islandKey = _tri.FlagsVersion;
                _islandVersion++;
            }
            var key = (_coverageVersion, _islandVersion, _paint.ContentVersion, margin);
            if (resized || key != _compositeKey)
            {
                Composite(margin);
                _compositeKey = key;
                Version++;
            }
        }

        private bool EnsureTargets(int width, int height)
        {
            bool lost = _mask == null || !_mask.IsCreated() || !_island.IsCreated() || !_coverage.IsCreated();
            if (!lost && width == _width && height == _height) return false;
            ReleaseTargets();
            _width = width;
            _height = height;
            _coverage = _gpu.CreateMaskRT("Coverage", width, height, TextureWrapMode.Clamp);
            _island = _gpu.CreateMaskRT("Island", width, height, TextureWrapMode.Clamp);
            _merged = _gpu.CreateMaskRT("Merged", width, height, TextureWrapMode.Clamp);
            _temp = _gpu.CreateMaskRT("Temp", width, height, TextureWrapMode.Clamp);
            _mask = _gpu.CreateMaskRT("Mask", width, height, TextureWrapMode.Repeat);
            return true;
        }

        private void DrawUV(RenderTexture target, int pass)
        {
            var m = _gpu.UVRaster;
            _tri.Bind(m);
            using (new RenderTargetScope(target))
            {
                GL.Clear(false, true, Color.clear);
                foreach (var offset in TriMesh.WrapOffsets(_wrap))
                {
                    m.SetVector(ShaderIds.UVOffset, offset);
                    m.SetPass(pass);
                    Graphics.DrawMeshNow(_tri.Mesh, Matrix4x4.identity);
                }
            }
        }

        private void Composite(int margin)
        {
            var m = _gpu.ImageOps;
            int n = Mathf.Clamp(Mathf.CeilToInt(PaintSurface.Size / (float)Mathf.Min(_width, _height)), 1, 8);
            m.SetVector(ShaderIds.OutSize, new Vector4(_width, _height, 1f / _width, 1f / _height));
            m.SetTexture(ShaderIds.PaintTex, _paint.Texture);
            m.SetTexture(ShaderIds.IslandTex, _island);
            m.SetFloat(ShaderIds.N, n);
            var prev = RenderTexture.active;
            Graphics.Blit(_gpu.Dummy, _merged, m, ShaderIds.OpDownsample);
            if (margin > 0)
            {
                m.SetFloat(ShaderIds.Margin, margin);
                m.SetTexture(ShaderIds.SrcTex, _merged);
                Graphics.Blit(_gpu.Dummy, _temp, m, ShaderIds.OpDilateH);
                m.SetTexture(ShaderIds.SrcTex, _temp);
                m.SetTexture(ShaderIds.MergedTex, _merged);
                m.SetTexture(ShaderIds.CoverageTex, _coverage);
                Graphics.Blit(_gpu.Dummy, _mask, m, ShaderIds.OpDilateV);
            }
            else
            {
                m.SetTexture(ShaderIds.SrcTex, _merged);
                Graphics.Blit(_gpu.Dummy, _mask, m, ShaderIds.OpCopy);
            }
            RenderTexture.active = prev;
        }

        /// <summary>D を読み戻す（0 / 255、行 0 = v 0）。</summary>
        public byte[] ReadMask() => GpuReadback.ReadR8(_mask);

        private void ReleaseTargets()
        {
            _gpu.Release(_coverage);
            _gpu.Release(_island);
            _gpu.Release(_merged);
            _gpu.Release(_temp);
            _gpu.Release(_mask);
            _coverage = _island = _merged = _temp = _mask = null;
        }

        public void Dispose() => ReleaseTargets();
    }
}
