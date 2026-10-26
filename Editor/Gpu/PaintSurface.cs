using Dennoko.UVMaskMaker.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 手塗りレイヤー 4096² の GPU 側（Design gpu_pipeline §5）。
    /// 真実は EditState.PaintRle で、ここはそのキャッシュ + 確定前のストローク。
    /// </summary>
    internal sealed class PaintSurface
    {
        public const int Size = EditState.PaintSize;

        private readonly GpuResources _gpu;
        private RenderTexture _rt;
        private Texture2D _upload;
        private byte[] _scratch;
        private int _gpuRevision = -1;

        /// <summary>GPU 側の内容が変わるたびに +1（合成のやり直し判定）。</summary>
        public int ContentVersion { get; private set; }
        /// <summary>確定していないストロークがある。</summary>
        public bool StrokeDirty { get; private set; }

        public PaintSurface(GpuResources gpu)
        {
            _gpu = gpu;
            _rt = gpu.CreateMaskRT("Paint", Size, Size, TextureWrapMode.Clamp);
        }

        public RenderTexture Texture => _rt;

        /// <summary>EditState の内容と一致させる（Undo / Redo / 作業開始 / デバイスロスト）。</summary>
        public void Ensure(EditState state)
        {
            if (!_rt.IsCreated())
            {
                _rt.Create();
                _gpuRevision = -1;
            }
            if (_gpuRevision == state.PaintRevision) return;
            Restore(state.PaintRle);
            _gpuRevision = state.PaintRevision;
            StrokeDirty = false;
        }

        private void Restore(byte[] rle)
        {
            if (PaintRle.IsEmpty(rle))
            {
                using (new RenderTargetScope(_rt)) GL.Clear(false, true, Color.clear);
            }
            else
            {
                _scratch ??= new byte[Size * Size];
                PaintRle.Decode(rle, _scratch);
                _upload ??= _gpu.CreateTexture("PaintUpload", Size, Size, TextureFormat.R8, true);
                _upload.LoadRawTextureData(_scratch);
                _upload.Apply(false, false);
                var m = _gpu.ImageOps;
                m.SetTexture(ShaderIds.SrcTex, _upload);
                var prev = RenderTexture.active;
                Graphics.Blit(_gpu.Dummy, _rt, m, ShaderIds.OpCopy);
                RenderTexture.active = prev;
            }
            ContentVersion++;
        }

        public void MarkPainted()
        {
            ContentVersion++;
            StrokeDirty = true;
        }

        /// <summary>GPU の内容を読み戻して圧縮する。</summary>
        public byte[] ReadAndEncode() => PaintRle.Encode(GpuReadback.ReadR8(_rt));

        /// <summary>EditState に確定した直後に呼ぶ（自分が書いた内容を読み直して転送し直さない）。</summary>
        public void AcceptCommitted(int revision)
        {
            _gpuRevision = revision;
            StrokeDirty = false;
        }

        /// <summary>確定前のストロークを捨てる。次の Ensure で EditState の内容に戻る。</summary>
        public void DiscardStroke()
        {
            if (!StrokeDirty) return;
            _gpuRevision = -1;
            StrokeDirty = false;
        }
    }

    /// <summary>RenderTexture の読み戻し（行 0 = v 0）。</summary>
    internal static class GpuReadback
    {
        public static byte[] ReadR8(RenderTexture rt)
        {
            bool r8 = rt.format == RenderTextureFormat.R8;
            var req = AsyncGPUReadback.Request(rt, 0, r8 ? TextureFormat.R8 : TextureFormat.RGBA32);
            req.WaitForCompletion();
            if (req.hasError) throw new System.InvalidOperationException("GPU readback failed");
            NativeArray<byte> data = req.GetData<byte>();
            if (r8) return data.ToArray();
            var result = new byte[rt.width * rt.height];
            for (int i = 0; i < result.Length; i++) result[i] = data[i * 4];
            return result;
        }

        public static byte[] ReadRGBA32(RenderTexture rt)
        {
            var req = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
            req.WaitForCompletion();
            if (req.hasError) throw new System.InvalidOperationException("GPU readback failed");
            return req.GetData<byte>().ToArray();
        }
    }
}
