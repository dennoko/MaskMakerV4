using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Core
{
    public enum SelectionGranularity
    {
        UVIsland = 0,
        ConnectedMesh = 1,
        Polygon = 2,
    }

    /// <summary>解析の入力。三角形は全サブメッシュ通しの連番（mesh.triangles の順）。</summary>
    public sealed class MeshInput
    {
        public Vector3[] Positions;            // [V] ローカル空間
        public Vector2[] UVs;                  // [V]
        public int[] Indices;                  // [3T]
        public int[] TriSubmesh;               // [T]
        public int SubmeshCount;
        public Vector3[] MirrorSpacePositions; // [V] 対称の基準空間。null なら対称表を作らない
        public float MirrorTolerance;          // <= 0 で自動

        public int TriangleCount => Indices.Length / 3;
        public int VertexCount => Positions.Length;
    }

    /// <summary>
    /// UV 空間の辺。サブメッシュごとに 1 本あり、Tri はそのサブメッシュでこの辺を持つ三角形の 1 つ。
    /// Repeat は同じ辺を別のサブメッシュで既に持っている印（全体を表示する時は重ねて描かない）。
    /// </summary>
    public struct UVEdge
    {
        public Vector2 A, B;
        public int Tri;
        public bool Repeat;

        /// <summary>filter（三角形のビット列）で絞った表示に含まれるか。null は全体。</summary>
        public bool VisibleIn(int[] filter) => filter == null ? !Repeat : TriangleBits.Get(filter, Tri);
    }

    /// <summary>3D の辺（作業メッシュの頂点番号）。Tri と Repeat は UVEdge と同じ。</summary>
    public struct VertexEdge
    {
        public int A, B;
        public int Tri;
        public bool Repeat;

        public bool VisibleIn(int[] filter) => filter == null ? !Repeat : TriangleBits.Get(filter, Tri);
    }

    /// <summary>グループの外周辺。3D 描画と UV 描画の両方に使う。</summary>
    public struct OutlineEdge
    {
        public int VertexA, VertexB;
        public Vector2 UVA, UVB;
    }

    /// <summary>解析結果。三角形の並びは入力と同じ。</summary>
    public sealed class MeshAnalysis
    {
        public int TriangleCount;
        public int VertexCount;
        public int SubmeshCount;
        public int[] Indices;
        public int[] TriSubmesh;
        public int[] PosIds;      // [V]
        public int[] PosUVIds;    // [V]

        public Vector2[] TriUV;   // [3T] 範囲外補正後
        public bool NeedsWrap;

        public GroupTable Islands;
        public GroupTable Parts;
        public GroupTable Polygons;

        public int[] AdjStart;    // [T+1]
        public int[] AdjTris;

        public List<UVEdge> BorderEdges;
        public List<UVEdge> WireEdges;
        public List<VertexEdge> SeamEdges3D;

        public int[] MirrorTri;   // [T] 無ければ -1。対称表を作らなかった時は null
        public float MirrorMatchRate;

        public ulong MeshHash;

        public GroupTable Groups(SelectionGranularity g)
        {
            switch (g)
            {
                case SelectionGranularity.ConnectedMesh: return Parts;
                case SelectionGranularity.Polygon: return Polygons;
                default: return Islands;
            }
        }
    }
}
