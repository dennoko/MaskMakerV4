namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>合成済みマスク D（0 / 非 0）を出力用の RGBA32 にする（Impl export §2.1）。</summary>
    public static class MaskEncoder
    {
        /// <summary>選択 = 黒、非選択 = 白。invert で入れ替え。</summary>
        public static byte[] Classic(byte[] mask, bool invert)
        {
            var rgba = new byte[mask.Length * 4];
            for (int i = 0; i < mask.Length; i++)
            {
                bool selected = (mask[i] > 127) != invert;
                byte v = selected ? (byte)0 : (byte)255;
                int o = i * 4;
                rgba[o] = v; rgba[o + 1] = v; rgba[o + 2] = v; rgba[o + 3] = 255;
            }
            return rgba;
        }

        /// <summary>
        /// チャンネル別書き込み。baseRgba があれば非選択画素は元の値を残す（mask と同じサイズであること）。
        /// </summary>
        public static byte[] Channel(byte[] mask, byte[] baseRgba, bool r, bool g, bool b, bool a, bool invert)
        {
            var rgba = new byte[mask.Length * 4];
            bool hasBase = baseRgba != null;
            for (int i = 0; i < mask.Length; i++)
            {
                int o = i * 4;
                bool selected = (mask[i] > 127) != invert;
                if (hasBase)
                {
                    rgba[o] = baseRgba[o]; rgba[o + 1] = baseRgba[o + 1]; rgba[o + 2] = baseRgba[o + 2]; rgba[o + 3] = baseRgba[o + 3];
                    if (!selected) continue;
                    if (r) rgba[o] = 0;
                    if (g) rgba[o + 1] = 0;
                    if (b) rgba[o + 2] = 0;
                    if (a) rgba[o + 3] = 255;
                }
                else
                {
                    byte v = selected ? (byte)0 : (byte)255;
                    rgba[o] = r ? v : (byte)255;
                    rgba[o + 1] = g ? v : (byte)255;
                    rgba[o + 2] = b ? v : (byte)255;
                    rgba[o + 3] = a ? v : (byte)255;
                }
            }
            return rgba;
        }
    }
}
