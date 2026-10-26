namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>三角形のビット列（32 三角形 / 要素）の基本操作。</summary>
    public static class TriangleBits
    {
        public static int WordCount(int triCount) => (triCount + 31) >> 5;

        public static int[] Create(int triCount) => new int[WordCount(triCount)];

        public static bool Get(int[] bits, int t) => (bits[t >> 5] & (1 << (t & 31))) != 0;

        public static void Set(int[] bits, int t, bool value)
        {
            if (value) bits[t >> 5] |= 1 << (t & 31);
            else bits[t >> 5] &= ~(1 << (t & 31));
        }

        /// <summary>対象サブメッシュに属する三角形のマスク。targetSubmesh &lt; 0 は全体。</summary>
        public static int[] ActiveMask(int[] triSubmesh, int targetSubmesh)
        {
            var mask = Create(triSubmesh.Length);
            for (int t = 0; t < triSubmesh.Length; t++)
                if (targetSubmesh < 0 || triSubmesh[t] == targetSubmesh) Set(mask, t, true);
            return mask;
        }

        public static int Count(int[] bits)
        {
            int n = 0;
            for (int i = 0; i < bits.Length; i++) n += PopCount((uint)bits[i]);
            return n;
        }

        public static bool AnySet(int[] bits)
        {
            for (int i = 0; i < bits.Length; i++) if (bits[i] != 0) return true;
            return false;
        }

        public static int[] Copy(int[] bits) => (int[])bits.Clone();

        private static int PopCount(uint v)
        {
            v = v - ((v >> 1) & 0x55555555u);
            v = (v & 0x33333333u) + ((v >> 2) & 0x33333333u);
            return (int)((((v + (v >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24);
        }
    }
}
