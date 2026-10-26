using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>
    /// UV 空間の三角形グリッド（Impl selection_system §6）。カーソル下の全三角形を返す。
    /// 0〜1 をはみ出す三角形は別リストに置き、回り込み位置でも判定する。
    /// </summary>
    public sealed class UVGrid
    {
        private readonly Vector2[] _triUV;
        private readonly int _res;
        private readonly int[] _cellStart;
        private readonly int[] _cellTris;
        private readonly List<int> _overflow = new List<int>();

        public UVGrid(Vector2[] triUV, int resolution = 128)
        {
            _triUV = triUV;
            _res = resolution;
            int triCount = triUV.Length / 3;
            int cells = _res * _res;
            var counts = new int[cells + 1];

            for (int pass = 0; pass < 2; pass++)
            {
                int[] cursor = null;
                if (pass == 1)
                {
                    for (int i = 0; i < cells; i++) counts[i + 1] += counts[i];
                    _cellStart = counts;
                    _cellTris = new int[counts[cells]];
                    cursor = new int[cells];
                    System.Array.Copy(counts, cursor, cells);
                }
                for (int t = 0; t < triCount; t++)
                {
                    if (pass == 0 && UVWrap.TriangleOutside(triUV, t)) _overflow.Add(t);
                    Bounds(t, out int x0, out int y0, out int x1, out int y1);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int cell = y * _res + x;
                            if (pass == 0) counts[cell + 1]++;
                            else _cellTris[cursor[cell]++] = t;
                        }
                }
            }
        }

        /// <summary>uv を含む有効三角形を result に追加する（重複なし）。</summary>
        public void Query(Vector2 uv, int[] activeBits, List<int> result)
        {
            result.Clear();
            if (uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f)
            {
                int x = Mathf.Clamp((int)(uv.x * _res), 0, _res - 1);
                int y = Mathf.Clamp((int)(uv.y * _res), 0, _res - 1);
                int cell = y * _res + x;
                for (int i = _cellStart[cell]; i < _cellStart[cell + 1]; i++)
                    TryAdd(_cellTris[i], uv, activeBits, result);
            }
            for (int k = 0; k < _overflow.Count; k++)
            {
                int t = _overflow[k];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dy != 0) TryAdd(t, uv + new Vector2(dx, dy), activeBits, result);
            }
        }

        private void TryAdd(int t, Vector2 p, int[] activeBits, List<int> result)
        {
            if (activeBits != null && !TriangleBits.Get(activeBits, t)) return;
            if (!Contains(_triUV[t * 3], _triUV[t * 3 + 1], _triUV[t * 3 + 2], p)) return;
            if (!result.Contains(t)) result.Add(t);
        }

        public static bool Contains(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        private void Bounds(int t, out int x0, out int y0, out int x1, out int y1)
        {
            Vector2 a = _triUV[t * 3], b = _triUV[t * 3 + 1], c = _triUV[t * 3 + 2];
            Vector2 min = Vector2.Min(a, Vector2.Min(b, c));
            Vector2 max = Vector2.Max(a, Vector2.Max(b, c));
            x0 = Mathf.Clamp((int)(min.x * _res), 0, _res - 1);
            y0 = Mathf.Clamp((int)(min.y * _res), 0, _res - 1);
            x1 = Mathf.Clamp((int)(max.x * _res), 0, _res - 1);
            y1 = Mathf.Clamp((int)(max.y * _res), 0, _res - 1);
        }
    }
}
