using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>各三角形の X 反転位置にある三角形を空間ハッシュで探す（Impl mesh_analysis §8）。</summary>
    public static class MirrorMapBuilder
    {
        public const float AutoToleranceRatio = 0.002f;

        public static int[] Build(Vector3[] positions, int[] indices, float tolerance, out float matchRate)
        {
            int triCount = indices.Length / 3;
            var mirror = new int[triCount];
            matchRate = 0f;
            if (triCount == 0) return mirror;

            var centroids = new Vector3[triCount];
            Vector3 min = positions[indices[0]], max = min;
            for (int t = 0; t < triCount; t++)
            {
                Vector3 c = (positions[indices[t * 3]] + positions[indices[t * 3 + 1]] + positions[indices[t * 3 + 2]]) / 3f;
                centroids[t] = c;
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }
            if (tolerance <= 0f)
            {
                Vector3 size = max - min;
                tolerance = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * AutoToleranceRatio;
                if (tolerance <= 0f) tolerance = 1e-5f;
            }

            float inv = 1f / tolerance;
            var head = new Dictionary<long, int>(triCount);
            var next = new int[triCount];
            for (int t = 0; t < triCount; t++)
            {
                long cell = CellKey(Cell(centroids[t], inv));
                next[t] = head.TryGetValue(cell, out int h) ? h : -1;
                head[cell] = t;
            }

            float tol2 = tolerance * tolerance;
            int matched = 0;
            for (int t = 0; t < triCount; t++)
            {
                Vector3 target = centroids[t];
                target.x = -target.x;
                Vector3Int baseCell = Cell(target, inv);
                int best = -1;
                float bestD = tol2;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    long cell = CellKey(baseCell + new Vector3Int(dx, dy, dz));
                    if (!head.TryGetValue(cell, out int i)) continue;
                    for (; i >= 0; i = next[i])
                    {
                        float d = (centroids[i] - target).sqrMagnitude;
                        if (d <= bestD) { bestD = d; best = i; }
                    }
                }
                mirror[t] = best;
                if (best >= 0) matched++;
            }
            matchRate = matched / (float)triCount;
            return mirror;
        }

        private static Vector3Int Cell(Vector3 p, float inv)
            => new Vector3Int(Mathf.FloorToInt(p.x * inv), Mathf.FloorToInt(p.y * inv), Mathf.FloorToInt(p.z * inv));

        private static long CellKey(Vector3Int c)
            => ((long)c.x * 73856093L) ^ ((long)c.y * 19349663L) ^ ((long)c.z * 83492791L);
    }
}
