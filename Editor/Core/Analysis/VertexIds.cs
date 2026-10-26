using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>位置・UV の量子化による正準頂点 ID（Impl mesh_analysis §3）。</summary>
    public static class VertexIds
    {
        private const int PosBits = 20;
        private const long PosMask = (1L << 21) - 1;
        private const float UVScale = 65536f;

        /// <summary>同じ位置の頂点に同じ ID を振る。</summary>
        public static int[] PositionIds(Vector3[] positions, out int count)
        {
            var ids = new int[positions.Length];
            count = 0;
            if (positions.Length == 0) return ids;

            Vector3 min = positions[0], max = positions[0];
            for (int i = 1; i < positions.Length; i++)
            {
                min = Vector3.Min(min, positions[i]);
                max = Vector3.Max(max, positions[i]);
            }
            Vector3 size = max - min;
            float extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float eps = extent > 0f ? extent / (1 << PosBits) : 1e-7f;
            float inv = 1f / eps;

            var map = new Dictionary<long, int>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                long key = PosKey(positions[i], min, inv);
                if (!map.TryGetValue(key, out int id))
                {
                    id = count++;
                    map.Add(key, id);
                }
                ids[i] = id;
            }
            return ids;
        }

        /// <summary>位置 ID と UV がどちらも同じ頂点に同じ ID を振る。</summary>
        public static int[] PositionUVIds(int[] posIds, Vector2[] uvs, out int count)
        {
            var ids = new int[posIds.Length];
            var map = new Dictionary<(int, int, int), int>(posIds.Length);
            count = 0;
            for (int i = 0; i < posIds.Length; i++)
            {
                var key = (posIds[i], Mathf.RoundToInt(uvs[i].x * UVScale), Mathf.RoundToInt(uvs[i].y * UVScale));
                if (!map.TryGetValue(key, out int id))
                {
                    id = count++;
                    map.Add(key, id);
                }
                ids[i] = id;
            }
            return ids;
        }

        public static long EdgeKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)hi << 32) | (uint)lo;
        }

        private static long PosKey(Vector3 p, Vector3 min, float inv)
        {
            long qx = Quantize(p.x - min.x, inv);
            long qy = Quantize(p.y - min.y, inv);
            long qz = Quantize(p.z - min.z, inv);
            return (qx << 42) | (qy << 21) | qz;
        }

        private static long Quantize(float v, float inv)
        {
            long q = (long)Mathf.Round(v * inv);
            if (q < 0) q = 0;
            if (q > PosMask) q = PosMask;
            return q;
        }
    }
}
