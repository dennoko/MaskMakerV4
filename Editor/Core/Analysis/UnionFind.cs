namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>経路半減とサイズによる結合の Union-Find。</summary>
    public sealed class UnionFind
    {
        private readonly int[] _parent;
        private readonly int[] _size;

        public UnionFind(int n)
        {
            _parent = new int[n];
            _size = new int[n];
            for (int i = 0; i < n; i++) { _parent[i] = i; _size[i] = 1; }
        }

        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a == b) return;
            if (_size[a] < _size[b]) { var tmp = a; a = b; b = tmp; }
            _parent[b] = a;
            _size[a] += _size[b];
        }
    }
}
