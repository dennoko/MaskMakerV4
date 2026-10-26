namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>CSR 形式のグループ表。グループ g の三角形は GroupTris[GroupStart[g] .. GroupStart[g+1])。</summary>
    public sealed class GroupTable
    {
        public int Count;
        public int[] GroupStart;  // [K+1]
        public int[] GroupTris;   // [T]
        public int[] TriToGroup;  // [T]

        public int SizeOf(int g) => GroupStart[g + 1] - GroupStart[g];

        /// <summary>Union-Find の結果から、三角形番号の小さい順の初出順に採番して作る。</summary>
        public static GroupTable FromUnionFind(UnionFind uf, int triCount)
        {
            var rootToGroup = new int[triCount];
            for (int i = 0; i < triCount; i++) rootToGroup[i] = -1;

            var table = new GroupTable { TriToGroup = new int[triCount] };
            int k = 0;
            for (int t = 0; t < triCount; t++)
            {
                int r = uf.Find(t);
                if (rootToGroup[r] < 0) rootToGroup[r] = k++;
                table.TriToGroup[t] = rootToGroup[r];
            }
            table.Count = k;
            table.FillCsr(triCount);
            return table;
        }

        /// <summary>三角形 1 つ = 1 グループ（Polygon）。</summary>
        public static GroupTable Identity(int triCount)
        {
            var table = new GroupTable
            {
                Count = triCount,
                TriToGroup = new int[triCount],
                GroupTris = new int[triCount],
                GroupStart = new int[triCount + 1],
            };
            for (int t = 0; t < triCount; t++)
            {
                table.TriToGroup[t] = t;
                table.GroupTris[t] = t;
                table.GroupStart[t] = t;
            }
            table.GroupStart[triCount] = triCount;
            return table;
        }

        private void FillCsr(int triCount)
        {
            GroupStart = new int[Count + 1];
            for (int t = 0; t < triCount; t++) GroupStart[TriToGroup[t] + 1]++;
            for (int g = 0; g < Count; g++) GroupStart[g + 1] += GroupStart[g];

            GroupTris = new int[triCount];
            var cursor = new int[Count];
            for (int g = 0; g < Count; g++) cursor[g] = GroupStart[g];
            for (int t = 0; t < triCount; t++) GroupTris[cursor[TriToGroup[t]]++] = t;
        }
    }
}
