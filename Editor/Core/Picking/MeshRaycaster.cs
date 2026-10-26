using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    public struct RayHit
    {
        public int Triangle;
        public float Distance;
        public Vector3 Point;        // レイと同じ空間
        public Vector3 Normal;       // 面法線（レイと同じ空間、正規化済み）
    }

    /// <summary>作業メッシュへの CPU レイキャスト（Impl selection_system §5）。裏面にも当たる。</summary>
    public sealed class MeshRaycaster
    {
        private readonly int[] _indices;
        private Vector3[] _vertices;

        public MeshRaycaster(int[] indices, Vector3[] vertices)
        {
            _indices = indices;
            _vertices = vertices;
        }

        public void SetVertices(Vector3[] vertices) => _vertices = vertices;

        /// <summary>有効三角形の中で最も近い交点。dir は正規化済みであること。</summary>
        public bool Raycast(Vector3 origin, Vector3 dir, int[] activeBits, out RayHit hit)
        {
            hit = default;
            hit.Triangle = -1;
            float best = float.MaxValue;
            int triCount = _indices.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                if (activeBits != null && !TriangleBits.Get(activeBits, t)) continue;
                Vector3 a = _vertices[_indices[t * 3]];
                Vector3 b = _vertices[_indices[t * 3 + 1]];
                Vector3 c = _vertices[_indices[t * 3 + 2]];
                if (Intersect(origin, dir, a, b, c, out float dist) && dist < best)
                {
                    best = dist;
                    hit.Triangle = t;
                    hit.Distance = dist;
                    hit.Normal = Vector3.Cross(b - a, c - a).normalized;
                }
            }
            if (hit.Triangle < 0) return false;
            hit.Point = origin + dir * hit.Distance;
            return true;
        }

        /// <summary>Möller–Trumbore（両面）。</summary>
        public static bool Intersect(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float dist)
        {
            dist = 0f;
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-12f && det < 1e-12f) return false;
            float inv = 1f / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            dist = Vector3.Dot(e2, q) * inv;
            return dist > 0f;
        }
    }
}
