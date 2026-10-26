using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// GPU 資源の生成と一括解放（Design state_and_lifecycle §5.4）。
    /// 名前は "UVMaskMaker/…"、HideFlags は HideAndDontSave。リロード後に SweepLeaks で取りこぼしを消す。
    /// </summary>
    internal sealed class GpuResources : IDisposable
    {
        public const string Prefix = "UVMaskMaker/";

        private readonly List<Object> _objects = new List<Object>();
        private readonly List<GraphicsBuffer> _buffers = new List<GraphicsBuffer>();

        public Material UVRaster { get; private set; }
        public Material ImageOps { get; private set; }
        public Material Overlay { get; private set; }
        public Material Line { get; private set; }
        public Texture2D Dummy { get; private set; }

        public static readonly RenderTextureFormat MaskFormat =
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

        public static bool TryCreate(out GpuResources res, out string error)
        {
            res = new GpuResources();
            error = null;
            res.UVRaster = res.CreateMaterial("Hidden/UVMaskMaker/UVRaster");
            res.ImageOps = res.CreateMaterial("Hidden/UVMaskMaker/ImageOps");
            res.Overlay = res.CreateMaterial("Hidden/UVMaskMaker/Overlay");
            res.Line = res.CreateMaterial("Hidden/UVMaskMaker/Line");
            if (res.UVRaster == null || res.ImageOps == null || res.Overlay == null || res.Line == null)
            {
                res.Dispose();
                res = null;
                error = L10n.T("error.shaderMissing");
                return false;
            }
            if (SystemInfo.graphicsShaderLevel < 45)
            {
                res.Dispose();
                res = null;
                error = L10n.T("error.gpuUnsupported");
                return false;
            }
            res.Dummy = res.CreateTexture("Dummy", 1, 1, TextureFormat.RGBA32, true);
            return true;
        }

        private Material CreateMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported) return null;
            return Track(new Material(shader), shaderName);
        }

        public RenderTexture CreateMaskRT(string name, int width, int height, TextureWrapMode wrap)
        {
            var rt = new RenderTexture(width, height, 0, MaskFormat, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Point,
                wrapMode = wrap,
                useMipMap = false,
                autoGenerateMips = false,
            };
            Track(rt, name);
            rt.Create();
            return rt;
        }

        public RenderTexture CreateColorRT(string name, int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            Track(rt, name);
            rt.Create();
            return rt;
        }

        public Texture2D CreateTexture(string name, int width, int height, TextureFormat format, bool linear)
        {
            var tex = new Texture2D(width, height, format, false, linear)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            return Track(tex, name);
        }

        public Mesh CreateMesh(string name) => Track(new Mesh(), name);

        public GraphicsBuffer CreateBuffer(int count, int stride)
        {
            var b = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), stride);
            _buffers.Add(b);
            return b;
        }

        public void Release(Object obj)
        {
            if (obj == null) return;
            _objects.Remove(obj);
            if (obj is RenderTexture rt) rt.Release();
            Object.DestroyImmediate(obj);
        }

        public void Release(GraphicsBuffer buffer)
        {
            if (buffer == null) return;
            _buffers.Remove(buffer);
            buffer.Dispose();
        }

        private T Track<T>(T obj, string name) where T : Object
        {
            obj.name = Prefix + name;
            obj.hideFlags = HideFlags.HideAndDontSave;
            _objects.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            foreach (var b in _buffers) b.Dispose();
            _buffers.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                var o = _objects[i];
                if (o == null) continue;
                if (o is RenderTexture rt) rt.Release();
                Object.DestroyImmediate(o);
            }
            _objects.Clear();
        }

        /// <summary>前のドメインで解放し損ねた資源を消す（リロード直後、まだ何も作っていない時に呼ぶ）。</summary>
        public static void SweepLeaks()
        {
            Sweep<RenderTexture>();
            Sweep<Texture2D>();
            Sweep<Material>();
            Sweep<Mesh>();
        }

        private static void Sweep<T>() where T : Object
        {
            foreach (var o in Resources.FindObjectsOfTypeAll<T>())
            {
                if (o == null || o.hideFlags != HideFlags.HideAndDontSave || !o.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                if (o is RenderTexture rt) rt.Release();
                Object.DestroyImmediate(o);
            }
        }
    }

    /// <summary>RenderTexture.active を保存・復元する。</summary>
    internal readonly struct RenderTargetScope : IDisposable
    {
        private readonly RenderTexture _previous;

        public RenderTargetScope(RenderTexture target)
        {
            _previous = RenderTexture.active;
            GL.PushMatrix();
            Graphics.SetRenderTarget(target);
        }

        public void Dispose()
        {
            GL.PopMatrix();
            RenderTexture.active = _previous;
        }
    }

    internal static class ShaderIds
    {
        public static readonly int TriFlags = Shader.PropertyToID("_TriFlags");
        public static readonly int TriColors = Shader.PropertyToID("_TriColors");
        public static readonly int UVOffset = Shader.PropertyToID("_UVOffset");
        public static readonly int PaintMode = Shader.PropertyToID("_PaintMode");
        public static readonly int SegA = Shader.PropertyToID("_SegA");
        public static readonly int SegB = Shader.PropertyToID("_SegB");
        public static readonly int Seg2 = Shader.PropertyToID("_Seg2");
        public static readonly int Radius = Shader.PropertyToID("_Radius");
        public static readonly int CamPos = Shader.PropertyToID("_CamPos");
        public static readonly int CamFwd = Shader.PropertyToID("_CamFwd");
        public static readonly int CamOrtho = Shader.PropertyToID("_CamOrtho");
        public static readonly int XRay = Shader.PropertyToID("_XRay");
        public static readonly int Mirror = Shader.PropertyToID("_Mirror");
        public static readonly int MirrorMatrix = Shader.PropertyToID("_MirrorMatrix");
        public static readonly int ShapeVP = Shader.PropertyToID("_ShapeVP");
        public static readonly int ShapeTex = Shader.PropertyToID("_ShapeTex");
        public static readonly int PaintValue = Shader.PropertyToID("_PaintValue");
        public static readonly int RequireSelected = Shader.PropertyToID("_RequireSelected");
        public static readonly int SrcTex = Shader.PropertyToID("_SrcTex");
        public static readonly int PaintTex = Shader.PropertyToID("_PaintTex");
        public static readonly int IslandTex = Shader.PropertyToID("_IslandTex");
        public static readonly int MergedTex = Shader.PropertyToID("_MergedTex");
        public static readonly int CoverageTex = Shader.PropertyToID("_CoverageTex");
        public static readonly int OutSize = Shader.PropertyToID("_OutSize");
        public static readonly int N = Shader.PropertyToID("_N");
        public static readonly int Margin = Shader.PropertyToID("_Margin");
        public static readonly int UVToView = Shader.PropertyToID("_UVToView");
        public static readonly int ViewSize = Shader.PropertyToID("_ViewSize");
        public static readonly int MaskTex = Shader.PropertyToID("_MaskTex");
        public static readonly int BaseTex = Shader.PropertyToID("_BaseTex");
        public static readonly int BaseAlpha = Shader.PropertyToID("_BaseAlpha");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int BgColor = Shader.PropertyToID("_BgColor");
        public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        public static readonly int UVToClip = Shader.PropertyToID("_UVToClip");
        public static readonly int Thickness = Shader.PropertyToID("_Thickness");
        public static readonly int TargetSize = Shader.PropertyToID("_TargetSize");

        // パス番号
        public const int RasterIsland = 0, RasterCoverage = 1, RasterPaintWorld = 2, RasterColorFill = 3;
        public const int OpDownsample = 0, OpDilateH = 1, OpDilateV = 2, OpPaintView = 3, OpImportOr = 4, OpCopy = 5;
        public const int OverlayScene = 0, OverlayUVView = 1;
        public const int LineWorld = 0, LineUV = 1;
    }
}
