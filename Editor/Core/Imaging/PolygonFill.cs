using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>
    /// 多角形を偶奇規則で 2 値画像に塗る（矩形・投げ縄の図形マスク用）。
    /// 画素 (x, y) は中心 (x+0.5, y+0.5) で判定し、dest[y * width + x] = 255 にする。
    /// 座標系（y の向き）は呼び出し側が決める。
    /// </summary>
    public static class PolygonFill
    {
        /// <summary>塗った画素があれば true。</summary>
        public static bool Fill(IReadOnlyList<Vector2> points, int width, int height, byte[] dest)
        {
            Array.Clear(dest, 0, dest.Length);
            int n = points.Count;
            if (n < 3) return false;

            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                minY = Mathf.Min(minY, points[i].y);
                maxY = Mathf.Max(maxY, points[i].y);
            }
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY), 0, height - 1);

            bool any = false;
            var xs = new List<float>(16);
            for (int y = y0; y <= y1; y++)
            {
                float cy = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % n];
                    if ((a.y <= cy) == (b.y <= cy)) continue;
                    xs.Add(a.x + (cy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Mathf.Max(0, Mathf.CeilToInt(xs[k] - 0.5f));
                    int xb = Mathf.Min(width - 1, Mathf.CeilToInt(xs[k + 1] - 0.5f) - 1);
                    int row = y * width;
                    for (int x = xa; x <= xb; x++) dest[row + x] = 255;
                    if (xb >= xa) any = true;
                }
            }
            return any;
        }

        public static List<Vector2> Rect(Vector2 a, Vector2 b)
        {
            return new List<Vector2> { a, new Vector2(b.x, a.y), b, new Vector2(a.x, b.y) };
        }
    }
}
