using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>UV 範囲外の三角形を整数分だけ平行移動する（Impl mesh_analysis §7）。</summary>
    public static class UVWrap
    {
        public static Vector2[] Apply(Vector2[] uvs, int[] indices, out bool needsWrap)
        {
            int triCount = indices.Length / 3;
            var triUV = new Vector2[indices.Length];
            needsWrap = false;
            for (int t = 0; t < triCount; t++)
            {
                Vector2 a = uvs[indices[t * 3]];
                Vector2 b = uvs[indices[t * 3 + 1]];
                Vector2 c = uvs[indices[t * 3 + 2]];
                Vector2 centroid = (a + b + c) / 3f;
                var shift = new Vector2(Mathf.Floor(centroid.x), Mathf.Floor(centroid.y));
                a -= shift; b -= shift; c -= shift;
                triUV[t * 3] = a;
                triUV[t * 3 + 1] = b;
                triUV[t * 3 + 2] = c;
                if (!needsWrap && (Outside(a) || Outside(b) || Outside(c))) needsWrap = true;
            }
            return triUV;
        }

        public static bool Outside(Vector2 p) => p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f;

        public static bool TriangleOutside(Vector2[] triUV, int t)
            => Outside(triUV[t * 3]) || Outside(triUV[t * 3 + 1]) || Outside(triUV[t * 3 + 2]);
    }
}
