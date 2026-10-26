using UnityEngine;
using UnityEngine.Rendering;

namespace Dennoko.UVMaskMaker.Core
{
    public struct RayHit
    {
        public int Triangle;
        public float Distance;
        public Vector3 Point;        // レイと同じ空間
        public Vector3 Normal;       // 面法線（レイと同じ空間、正規化済み）
    }

    /// <summary>
    /// 作業メッシュへの CPU レイキャスト（Impl selection_system §5）。
    /// サブメッシュごとのカリング（マテリアルの Cull）で、シーンに描かれない面には当たらない。
    /// </summary>
    public sealed class MeshRaycaster
    {
        // 重心座標の許容幅。隣り合う三角形の共有辺でレイがすり抜けないようにする
        private const float EdgeEpsilon = 1e-5f;
        // 両面のマテリアルで表と裏がほぼ同じ距離にある時、表を優先するための裏面の割増（距離に対する比）
        private const float BackFaceBias = 1e-4f;

        private readonly int[] _indices;
        private readonly int[] _triSubmesh;
        private Vector3[] _vertices;

        /// <summary>サブメッシュごとのカリング。null または範囲外のサブメッシュは Back（裏面に当たらない）。</summary>
        public CullMode[] SubmeshCull { get; set; }

        public MeshRaycaster(int[] indices, Vector3[] vertices, int[] triSubmesh = null)
        {
            _indices = indices;
            _vertices = vertices;
            _triSubmesh = triSubmesh;
        }

        public void SetVertices(Vector3[] vertices) => _vertices = vertices;

        /// <summary>有効三角形の中で、描かれる面との最も近い交点。dir は正規化済みであること。</summary>
        public bool Raycast(Vector3 origin, Vector3 dir, int[] activeBits, out RayHit hit)
        {
            hit = default;
            hit.Triangle = -1;
            float bestKey = float.MaxValue;
            int triCount = _indices.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                if (activeBits != null && !TriangleBits.Get(activeBits, t)) continue;
                Vector3 a = _vertices[_indices[t * 3]];
                Vector3 b = _vertices[_indices[t * 3 + 1]];
                Vector3 c = _vertices[_indices[t * 3 + 2]];
                if (!Intersect(origin, dir, a, b, c, out float dist, out bool front)) continue;
                var cull = CullOf(t);
                if (front ? cull == CullMode.Front : cull == CullMode.Back) continue;
                float key = front ? dist : dist * (1f + BackFaceBias);
                if (key < bestKey)
                {
                    bestKey = key;
                    hit.Triangle = t;
                    hit.Distance = dist;
                    hit.Normal = Vector3.Cross(b - a, c - a).normalized;
                }
            }
            if (hit.Triangle < 0) return false;
            hit.Point = origin + dir * hit.Distance;
            return true;
        }

        private CullMode CullOf(int t)
        {
            var cull = SubmeshCull;
            if (cull == null || _triSubmesh == null) return CullMode.Back;
            int s = _triSubmesh[t];
            return s >= 0 && s < cull.Length ? cull[s] : CullMode.Back;
        }

        /// <summary>
        /// Möller–Trumbore（両面）。front は Unity の表面（時計回りに見える側）から当たったか。
        /// 辺の上は両隣の三角形に当たる（すり抜け防止）。
        /// </summary>
        public static bool Intersect(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float dist, out bool front)
        {
            dist = 0f;
            front = false;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-12f && det < 1e-12f) return false;
            float inv = 1f / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < -EdgeEpsilon || u > 1f + EdgeEpsilon) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < -EdgeEpsilon || u + v > 1f + EdgeEpsilon) return false;
            dist = Vector3.Dot(e2, q) * inv;
            front = det > 0f;
            return dist > 0f;
        }
    }
}
