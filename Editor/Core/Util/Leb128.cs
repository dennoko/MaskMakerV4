using System.Collections.Generic;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>符号なし LEB128 可変長整数。</summary>
    public static class Leb128
    {
        public static void Write(List<byte> dst, uint value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                dst.Add(b);
            } while (value != 0);
        }

        /// <summary>pos から 1 つ読み、pos を進める。壊れたデータでは false。</summary>
        public static bool TryRead(byte[] src, ref int pos, out uint value)
        {
            value = 0;
            int shift = 0;
            while (pos < src.Length && shift < 35)
            {
                byte b = src[pos++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
            }
            return false;
        }
    }
}
