using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 画像を元ファイルのバイト列から読む（Impl export §2.1、mask_processing §7.1）。
    /// インポート設定（圧縮・sRGB・最大サイズ）の影響を受けない。PNG / JPG 以外はインポート済みテクスチャから読む。
    /// </summary>
    internal static class SourceImage
    {
        /// <summary>RGBA32（行 0 = 下端）。</summary>
        public static bool TryLoadRgba(string assetPath, out byte[] rgba, out int width, out int height)
        {
            rgba = null;
            width = height = 0;
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return false;
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    if (!tex.LoadImage(File.ReadAllBytes(assetPath), false)) return false;
                    width = tex.width;
                    height = tex.height;
                    rgba = ToBytes(tex.GetPixels32());
                    return true;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            return TryReadImported(assetPath, out rgba, out width, out height);
        }

        /// <summary>PSD / TGA などはインポート済みテクスチャを sRGB の RT へ写して読む（圧縮の劣化は残る）。</summary>
        private static bool TryReadImported(string assetPath, out byte[] rgba, out int width, out int height)
        {
            rgba = null;
            width = height = 0;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return false;
            width = tex.width;
            height = tex.height;
            bool srgb = !(AssetImporter.GetAtPath(assetPath) is TextureImporter ti) || ti.sRGBTexture;
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            try
            {
                var prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = prev;
                rgba = GpuReadback.ReadRGBA32(rt);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>インポート前の元画像のサイズ（最大サイズで縮小されていない値）。</summary>
        public static bool TryGetSourceSize(Texture2D tex, out int width, out int height)
        {
            width = height = 0;
            if (tex == null) return false;
            var path = AssetDatabase.GetAssetPath(tex);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.GetSourceTextureWidthAndHeight(out width, out height);
                if (width > 0 && height > 0) return true;
            }
            width = tex.width;
            height = tex.height;
            return width > 0 && height > 0;
        }

        public static byte[] ToBytes(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                int o = i * 4;
                bytes[o] = pixels[i].r;
                bytes[o + 1] = pixels[i].g;
                bytes[o + 2] = pixels[i].b;
                bytes[o + 3] = pixels[i].a;
            }
            return bytes;
        }

        /// <summary>最近傍で拡大縮小する（RGBA32）。</summary>
        public static byte[] ResizeNearest(byte[] rgba, int w, int h, int newW, int newH)
        {
            if (w == newW && h == newH) return rgba;
            var dst = new byte[newW * newH * 4];
            for (int y = 0; y < newH; y++)
            {
                int sy = Mathf.Min(h - 1, (int)((y + 0.5f) * h / newH));
                for (int x = 0; x < newW; x++)
                {
                    int sx = Mathf.Min(w - 1, (int)((x + 0.5f) * w / newW));
                    Buffer.BlockCopy(rgba, (sy * w + sx) * 4, dst, (y * newW + x) * 4, 4);
                }
            }
            return dst;
        }
    }
}
