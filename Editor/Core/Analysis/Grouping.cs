using System.Collections.Generic;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>島・パーツのグループ化と外周（Impl mesh_analysis §6）。</summary>
    public static class Grouping
    {
        public static GroupTable BuildIslands(MeshAnalysis a)
        {
            var uf = new UnionFind(a.TriangleCount);
            var first = new Dictionary<(long, int), int>(a.TriangleCount * 2);
            for (int o = 0; o < a.TriangleCount * 3; o++)
            {
                int t = o / 3;
                EdgeTable.VertsOf(a, o, out int va, out int vb);
                var key = (VertexIds.EdgeKey(a.PosUVIds[va], a.PosUVIds[vb]), a.TriSubmesh[t]);
                if (first.TryGetValue(key, out int other)) uf.Union(t, other);
                else first.Add(key, t);
            }
            return GroupTable.FromUnionFind(uf, a.TriangleCount);
        }

        public static GroupTable BuildParts(MeshAnalysis a)
        {
            var uf = new UnionFind(a.TriangleCount);
            var first = new Dictionary<(int, int), int>(a.VertexCount);
            for (int i = 0; i < a.Indices.Length; i++)
            {
                int t = i / 3;
                var key = (a.PosIds[a.Indices[i]], a.TriSubmesh[t]);
                if (first.TryGetValue(key, out int other)) uf.Union(t, other);
                else first.Add(key, t);
            }
            return GroupTable.FromUnionFind(uf, a.TriangleCount);
        }

        /// <summary>グループ内で 1 回しか現れない辺（＝外周）。粒度によって「同じ辺」の判定が変わる。</summary>
        public static List<OutlineEdge> Outline(MeshAnalysis a, SelectionGranularity granularity, int group)
        {
            var table = a.Groups(granularity);
            var result = new List<OutlineEdge>();
            int start = table.GroupStart[group], end = table.GroupStart[group + 1];

            if (granularity == SelectionGranularity.Polygon)
            {
                for (int i = start; i < end; i++)
                {
                    int t = table.GroupTris[i];
                    for (int c = 0; c < 3; c++) result.Add(MakeOutline(a, t * 3 + c));
                }
                return result;
            }

            int[] ids = granularity == SelectionGranularity.UVIsland ? a.PosUVIds : a.PosIds;
            var counts = new Dictionary<long, int>();
            for (int i = start; i < end; i++)
            {
                int t = table.GroupTris[i];
                for (int c = 0; c < 3; c++)
                {
                    long k = Key(a, ids, t * 3 + c);
                    counts.TryGetValue(k, out int n);
                    counts[k] = n + 1;
                }
            }
            for (int i = start; i < end; i++)
            {
                int t = table.GroupTris[i];
                for (int c = 0; c < 3; c++)
                {
                    int o = t * 3 + c;
                    if (counts[Key(a, ids, o)] == 1) result.Add(MakeOutline(a, o));
                }
            }
            return result;
        }

        private static long Key(MeshAnalysis a, int[] ids, int o)
        {
            EdgeTable.VertsOf(a, o, out int va, out int vb);
            return VertexIds.EdgeKey(ids[va], ids[vb]);
        }

        private static OutlineEdge MakeOutline(MeshAnalysis a, int o)
        {
            int t = o / 3, c = o % 3;
            EdgeTable.VertsOf(a, o, out int va, out int vb);
            return new OutlineEdge
            {
                VertexA = va,
                VertexB = vb,
                UVA = a.TriUV[t * 3 + c],
                UVB = a.TriUV[t * 3 + (c + 1) % 3],
            };
        }
    }
}
