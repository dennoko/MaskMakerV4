using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Tests
{
    /// <summary>テスト用の小さなメッシュ。</summary>
    internal static class TestMeshes
    {
        /// <summary>UV で並んだ 2 枚の四角形（各 2 三角形）。separateUV=true なら共有辺が UV シーム。</summary>
        public static MeshInput TwoQuads(bool separateUV, bool secondSubmesh = false)
        {
            var pos = new List<Vector3>();
            var uv = new List<Vector2>();
            var idx = new List<int>();
            // 左の四角形 x:0..1、右の四角形 x:1..2（位置は共有辺 x=1 でつながる）
            AddQuad(pos, uv, idx, 0f, new Vector2(0f, 0f));
            AddQuad(pos, uv, idx, 1f, separateUV ? new Vector2(0.5f, 0f) : new Vector2(0.25f, 0f));
            var input = new MeshInput
            {
                Positions = pos.ToArray(),
                UVs = uv.ToArray(),
                Indices = idx.ToArray(),
                SubmeshCount = secondSubmesh ? 2 : 1,
            };
            input.TriSubmesh = new int[input.TriangleCount];
            if (secondSubmesh) { input.TriSubmesh[2] = 1; input.TriSubmesh[3] = 1; }
            return input;
        }

        private static void AddQuad(List<Vector3> pos, List<Vector2> uv, List<int> idx, float x, Vector2 uvOffset)
        {
            int b = pos.Count;
            pos.Add(new Vector3(x, 0, 0)); pos.Add(new Vector3(x + 1, 0, 0));
            pos.Add(new Vector3(x + 1, 1, 0)); pos.Add(new Vector3(x, 1, 0));
            const float s = 0.25f;
            uv.Add(uvOffset + new Vector2(0, 0)); uv.Add(uvOffset + new Vector2(s, 0));
            uv.Add(uvOffset + new Vector2(s, s)); uv.Add(uvOffset + new Vector2(0, s));
            idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
            idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
        }

        /// <summary>単位立方体。面ごとに頂点を分けて UV も面ごとに分ける（6 島、1 パーツ）。</summary>
        public static MeshInput Cube()
        {
            var pos = new List<Vector3>();
            var uv = new List<Vector2>();
            var idx = new List<int>();
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 normal = n[f];
                Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                int b = pos.Count;
                Vector3 c = normal * 0.5f;
                pos.Add(c - u * 0.5f - v * 0.5f); pos.Add(c + u * 0.5f - v * 0.5f);
                pos.Add(c + u * 0.5f + v * 0.5f); pos.Add(c - u * 0.5f + v * 0.5f);
                var o = new Vector2((f % 3) * 0.3f, (f / 3) * 0.3f);
                uv.Add(o); uv.Add(o + new Vector2(0.25f, 0)); uv.Add(o + new Vector2(0.25f, 0.25f)); uv.Add(o + new Vector2(0, 0.25f));
                idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
            }
            var input = new MeshInput { Positions = pos.ToArray(), UVs = uv.ToArray(), Indices = idx.ToArray(), SubmeshCount = 1 };
            input.TriSubmesh = new int[input.TriangleCount];
            return input;
        }
    }
}
