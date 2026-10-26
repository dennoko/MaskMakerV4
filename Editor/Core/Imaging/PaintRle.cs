using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>
    /// 2 値画像の連長圧縮（Impl mask_processing §3.4）。
    /// 値 0 から始まる「0 の連長、1 の連長、…」を交互に LEB128 で並べる。0 以外の画素は 1 とみなす。
    /// </summary>
    public static class PaintRle
    {
        public static byte[] Encode(byte[] pixels)
        {
            var dst = new List<byte>(1024);
            var words = MemoryMarshal.Cast<byte, ulong>(pixels.AsSpan());
            int n = pixels.Length;
            int i = 0;
            bool current = false;
            while (i < n)
            {
                int start = i;
                ulong uniform = current ? ulong.MaxValue : 0UL;
                // 8 画素単位で同じ値の並びを読み飛ばす
                while ((i & 7) != 0 && i < n && (pixels[i] != 0) == current) i++;
                if ((i & 7) == 0)
                {
                    int w = i >> 3;
                    while (w < words.Length && IsUniform(words[w], current)) w++;
                    i = Math.Max(i, w << 3);
                }
                while (i < n && (pixels[i] != 0) == current) i++;
                Leb128.Write(dst, (uint)(i - start));
                current = !current;
            }
            if (n == 0) Leb128.Write(dst, 0);
            return dst.ToArray();

            static bool IsUniform(ulong word, bool on) => on ? IsAllNonZero(word) : word == 0UL;
        }

        private static bool IsAllNonZero(ulong w)
        {
            for (int k = 0; k < 8; k++)
                if (((w >> (k * 8)) & 0xFF) == 0) return false;
            return true;
        }

        /// <summary>dest（長さ = 画素数）へ 0 / 255 で展開する。壊れたデータでは false（dest は全 0）。</summary>
        public static bool Decode(byte[] rle, byte[] dest)
        {
            var span = dest.AsSpan();
            span.Clear();
            if (rle == null || rle.Length == 0) return true;
            int pos = 0, offset = 0;
            bool current = false;
            while (pos < rle.Length)
            {
                if (!Leb128.TryRead(rle, ref pos, out uint run) || offset + (long)run > dest.Length)
                {
                    span.Clear();
                    return false;
                }
                if (current) span.Slice(offset, (int)run).Fill(255);
                offset += (int)run;
                current = !current;
            }
            return true;
        }

        /// <summary>全画素 0 を表すか（空のバイト列も空とみなす）。</summary>
        public static bool IsEmpty(byte[] rle)
        {
            if (rle == null || rle.Length == 0) return true;
            int pos = 0;
            if (!Leb128.TryRead(rle, ref pos, out _)) return true;
            return pos >= rle.Length;
        }

        public static byte[] Empty(int pixelCount)
        {
            var dst = new List<byte>(8);
            Leb128.Write(dst, (uint)pixelCount);
            return dst.ToArray();
        }
    }
}
