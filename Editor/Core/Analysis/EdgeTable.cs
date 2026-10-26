using System;
using System.Collections.Generic;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>
    /// 辺の集計（Impl mesh_analysis §5）。三角形の辺を位置の EdgeKey で並べ替え、
    /// 同じ位置の辺ごとに境界・シーム・三角形隣接を求める。
    /// </summary>
    public static class EdgeTable
    {
        public static void Build(MeshAnalysis a)
        {
            int triCount = a.TriangleCount;
            int edgeCount = triCount * 3;
            var keys = new long[edgeCount];
            var occ = new int[edgeCount];
            for (int o = 0; o < edgeCount; o++)
            {
                VertsOf(a, o, out int va, out int vb);
                keys[o] = VertexIds.EdgeKey(a.PosIds[va], a.PosIds[vb]);
                occ[o] = o;
            }
            Array.Sort(keys, occ);

            a.BorderEdges = new List<UVEdge>();
            a.SeamEdges3D = new List<VertexEdge>();
            a.WireEdges = BuildWireEdges(a);

            var degree = new int[triCount + 1];
            ForEachRun(keys, (s, e) => CountAdjacency(occ, s, e, degree));
            a.AdjStart = new int[triCount + 1];
            for (int t = 0; t < triCount; t++) a.AdjStart[t + 1] = a.AdjStart[t] + degree[t];
            a.AdjTris = new int[a.AdjStart[triCount]];
            var cursor = new int[triCount];
            Array.Copy(a.AdjStart, cursor, triCount);

            var added = new List<long>(4);
            ForEachRun(keys, (s, e) =>
            {
                FillAdjacency(a, occ, s, e, cursor);
                CollectBorders(a, occ, s, e, added);
            });
        }

        private static void ForEachRun(long[] keys, Action<int, int> body)
        {
            int s = 0;
            while (s < keys.Length)
            {
                int e = s + 1;
                while (e < keys.Length && keys[e] == keys[s]) e++;
                body(s, e);
                s = e;
            }
        }

        private static void CountAdjacency(int[] occ, int s, int e, int[] degree)
        {
            for (int i = s; i < e; i++)
                for (int j = s; j < e; j++)
                    if (occ[i] / 3 != occ[j] / 3) degree[occ[i] / 3]++;
        }

        private static void FillAdjacency(MeshAnalysis a, int[] occ, int s, int e, int[] cursor)
        {
            for (int i = s; i < e; i++)
                for (int j = s; j < e; j++)
                {
                    int ti = occ[i] / 3, tj = occ[j] / 3;
                    if (ti != tj) a.AdjTris[cursor[ti]++] = tj;
                }
        }

        private static void CollectBorders(MeshAnalysis a, int[] occ, int s, int e, List<long> added)
        {
            bool anyBorder = false;
            added.Clear(); // この位置で前のサブメッシュが追加した UV の辺（同じ辺に Repeat を付ける）
            // 同じ位置の辺の中で、サブメッシュごとに判定する（run は通常 2 本なので総当たりで十分）
            for (int i = s; i < e; i++)
            {
                int sub = a.TriSubmesh[occ[i] / 3];
                bool firstOfSubmesh = true;
                for (int j = s; j < i; j++)
                    if (a.TriSubmesh[occ[j] / 3] == sub) { firstOfSubmesh = false; break; }
                if (!firstOfSubmesh) continue;

                int count = 0;
                long firstUVKey = 0;
                bool multipleUV = false;
                for (int j = i; j < e; j++)
                {
                    if (a.TriSubmesh[occ[j] / 3] != sub) continue;
                    long k = PosUVKey(a, occ[j]);
                    if (count == 0) firstUVKey = k;
                    else if (k != firstUVKey) multipleUV = true;
                    count++;
                }
                if (count != 1 && !multipleUV) continue;

                VertsOf(a, occ[i], out int va, out int vb);
                a.SeamEdges3D.Add(new VertexEdge { A = va, B = vb, Tri = occ[i] / 3, Repeat = anyBorder });
                anyBorder = true;

                int addedBefore = added.Count;
                for (int j = i; j < e; j++)
                {
                    if (a.TriSubmesh[occ[j] / 3] != sub) continue;
                    long k = PosUVKey(a, occ[j]);
                    bool seen = false;
                    for (int m = i; m < j; m++)
                        if (a.TriSubmesh[occ[m] / 3] == sub && PosUVKey(a, occ[m]) == k) { seen = true; break; }
                    if (seen) continue;
                    var edge = MakeUVEdge(a, occ[j]);
                    int at = added.IndexOf(k);
                    edge.Repeat = at >= 0 && at < addedBefore;
                    a.BorderEdges.Add(edge);
                    added.Add(k);
                }
            }
        }

        private static List<UVEdge> BuildWireEdges(MeshAnalysis a)
        {
            var seen = new HashSet<long>();
            var seenInSubmesh = new HashSet<(int, long)>();
            var list = new List<UVEdge>(a.TriangleCount * 3 / 2);
            for (int o = 0; o < a.TriangleCount * 3; o++)
            {
                long k = PosUVKey(a, o);
                if (!seenInSubmesh.Add((a.TriSubmesh[o / 3], k))) continue;
                var edge = MakeUVEdge(a, o);
                edge.Repeat = !seen.Add(k);
                list.Add(edge);
            }
            return list;
        }

        private static long PosUVKey(MeshAnalysis a, int o)
        {
            VertsOf(a, o, out int va, out int vb);
            return VertexIds.EdgeKey(a.PosUVIds[va], a.PosUVIds[vb]);
        }

        private static UVEdge MakeUVEdge(MeshAnalysis a, int o)
        {
            int t = o / 3, c = o % 3;
            return new UVEdge { A = a.TriUV[t * 3 + c], B = a.TriUV[t * 3 + (c + 1) % 3], Tri = t };
        }

        /// <summary>辺の出現 o（= 三角形 * 3 + 角）の両端の頂点番号。</summary>
        public static void VertsOf(MeshAnalysis a, int o, out int va, out int vb)
        {
            int t = o / 3, c = o % 3;
            va = a.Indices[t * 3 + c];
            vb = a.Indices[t * 3 + (c + 1) % 3];
        }
    }
}
