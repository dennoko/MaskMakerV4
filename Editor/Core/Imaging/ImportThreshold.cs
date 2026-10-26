namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>既存マスクの黒い部分を判定する（Impl mask_processing §7.2）。</summary>
    public static class ImportThreshold
    {
        /// <summary>RGBA32（ファイルに書かれた値そのもの）から 0 / 255 の 2 値画像を作る。</summary>
        public static byte[] Apply(byte[] rgba, int threshold, out int blackCount)
        {
            int n = rgba.Length / 4;
            var dst = new byte[n];
            blackCount = 0;
            float limit = threshold;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                if (rgba[o + 3] < 128) continue;
                float lum = 0.299f * rgba[o] + 0.587f * rgba[o + 1] + 0.114f * rgba[o + 2];
                if (lum >= limit) continue;
                dst[i] = 255;
                blackCount++;
            }
            return dst;
        }
    }
}
