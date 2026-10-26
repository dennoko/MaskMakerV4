namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>64bit FNV-1a。作業状態の照合や変更検知に使う軽量ハッシュ。</summary>
    public struct Fnv1a
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _h;
        private bool _started;

        public ulong Value => _started ? _h : Offset;

        public void Add(byte b)
        {
            if (!_started) { _h = Offset; _started = true; }
            _h ^= b;
            _h *= Prime;
        }

        public void Add(int v)
        {
            Add((byte)v); Add((byte)(v >> 8)); Add((byte)(v >> 16)); Add((byte)(v >> 24));
        }

        public void Add(int[] values)
        {
            if (values == null) { Add(-1); return; }
            Add(values.Length);
            for (int i = 0; i < values.Length; i++) Add(values[i]);
        }

        public void Add(byte[] values)
        {
            if (values == null) { Add(-1); return; }
            Add(values.Length);
            for (int i = 0; i < values.Length; i++) Add(values[i]);
        }

        public static ulong Of(int[] values) { var h = new Fnv1a(); h.Add(values); return h.Value; }
        public static ulong Of(byte[] values) { var h = new Fnv1a(); h.Add(values); return h.Value; }
    }
}
