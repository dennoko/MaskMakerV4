using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 三角形ごとに頂点を分けたメッシュと三角形フラグ（Design gpu_pipeline §2.1）。
    /// POSITION / NORMAL = ローカル位置・法線、TEXCOORD0 = 補正後 UV、TEXCOORD1.x = 三角形番号。
    /// </summary>
    internal sealed class TriMesh
    {
        private readonly GpuResources _gpu;
        private readonly int[] _indices;
        private readonly uint[] _flagData;

        public Mesh Mesh { get; }
        public GraphicsBuffer Flags { get; }
        public int TriangleCount { get; }

        /// <summary>選択または有効三角形が変わるたびに +1。</summary>
        public int FlagsVersion { get; private set; }
        /// <summary>有効三角形（対象サブメッシュ）が変わるたびに +1。</summary>
        public int ActiveVersion { get; private set; }

        public TriMesh(GpuResources gpu, MeshAnalysis analysis, WorkMesh work)
        {
            _gpu = gpu;
            _indices = analysis.Indices;
            TriangleCount = analysis.TriangleCount;
            int n = TriangleCount * 3;

            Mesh = gpu.CreateMesh("TriMesh");
            Mesh.indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            Mesh.MarkDynamic();
            UpdatePose(work);
            Mesh.SetUVs(0, analysis.TriUV);
            var tri = new Vector2[n];
            for (int i = 0; i < n; i++) tri[i] = new Vector2(i / 3, 0f);
            Mesh.SetUVs(1, tri);
            var seq = new int[n];
            for (int i = 0; i < n; i++) seq[i] = i;
            Mesh.SetIndices(seq, MeshTopology.Triangles, 0, false);
            Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);

            Flags = gpu.CreateBuffer(TriangleCount, sizeof(uint));
            _flagData = new uint[Mathf.Max(1, TriangleCount)];
        }

        public void UpdatePose(WorkMesh work)
        {
            int n = _indices.Length;
            var pos = new Vector3[n];
            var nrm = new Vector3[n];
            var v = work.Vertices;
            var nv = work.Normals;
            for (int i = 0; i < n; i++)
            {
                pos[i] = v[_indices[i]];
                nrm[i] = nv[_indices[i]];
            }
            Mesh.SetVertices(pos);
            Mesh.SetNormals(nrm);
        }

        public void UpdateFlags(int[] selected, int[] active, bool activeChanged)
        {
            for (int t = 0; t < TriangleCount; t++)
            {
                uint f = 0;
                if (TriangleBits.Get(selected, t)) f |= 1u;
                if (TriangleBits.Get(active, t)) f |= 2u;
                _flagData[t] = f;
            }
            Flags.SetData(_flagData);
            FlagsVersion++;
            if (activeChanged) ActiveVersion++;
        }

        public void Bind(Material m) => m.SetBuffer(ShaderIds.TriFlags, Flags);

        /// <summary>UV 回り込みの描画オフセット（Design gpu_pipeline §3.2）。</summary>
        public static IEnumerable<Vector4> WrapOffsets(bool needsWrap)
        {
            yield return Vector4.zero;
            if (!needsWrap) yield break;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    if (x != 0 || y != 0) yield return new Vector4(x, y, 0, 0);
        }
    }

    /// <summary>太線用メッシュ（線分ごとに 4 頂点）。POSITION = A、TEXCOORD0 = B、TEXCOORD1 = (side, end)。</summary>
    internal sealed class LineMesh
    {
        public Mesh Mesh { get; }
        public int SegmentCount { get; private set; }

        public LineMesh(GpuResources gpu, string name)
        {
            Mesh = gpu.CreateMesh(name);
            Mesh.MarkDynamic();
        }

        public void Set(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b)
        {
            int n = a.Count;
            SegmentCount = n;
            var pos = new Vector3[n * 4];
            var other = new Vector3[n * 4];
            var param = new Vector2[n * 4];
            var idx = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int v = i * 4;
                for (int k = 0; k < 4; k++)
                {
                    pos[v + k] = a[i];
                    other[v + k] = b[i];
                }
                param[v] = new Vector2(-1, 0);
                param[v + 1] = new Vector2(1, 0);
                param[v + 2] = new Vector2(-1, 1);
                param[v + 3] = new Vector2(1, 1);
                int j = i * 6;
                idx[j] = v; idx[j + 1] = v + 1; idx[j + 2] = v + 2;
                idx[j + 3] = v + 2; idx[j + 4] = v + 1; idx[j + 5] = v + 3;
            }
            Mesh.Clear();
            Mesh.indexFormat = pos.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            Mesh.SetVertices(pos);
            Mesh.SetUVs(0, other);
            Mesh.SetUVs(1, param);
            Mesh.SetIndices(idx, MeshTopology.Triangles, 0, false);
            Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
        }

        /// <summary>UV 空間の辺から作る（z = 0）。filterBits の三角形の辺だけ。null は全体。</summary>
        public void SetUV(IReadOnlyList<UVEdge> edges, int[] filterBits = null)
        {
            var a = new List<Vector3>(edges.Count);
            var b = new List<Vector3>(edges.Count);
            foreach (var e in edges)
            {
                if (!e.VisibleIn(filterBits)) continue;
                a.Add(e.A);
                b.Add(e.B);
            }
            Set(a, b);
        }
    }
}
