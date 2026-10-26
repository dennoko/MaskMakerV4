using Dennoko.UVMaskMaker.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Tests
{
    public class AnalysisTests
    {
        [Test]
        public void ConnectedQuads_FormOneIslandAndOnePart()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: false));
            Assert.AreEqual(1, a.Islands.Count);
            Assert.AreEqual(1, a.Parts.Count);
            Assert.AreEqual(4, a.Polygons.Count);
            Assert.AreEqual(6, a.BorderEdges.Count);
            Assert.AreEqual(6, a.SeamEdges3D.Count);
        }

        [Test]
        public void UVSeam_SplitsIslandButNotPart()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: true));
            Assert.AreEqual(2, a.Islands.Count);
            Assert.AreEqual(1, a.Parts.Count);
            Assert.AreEqual(8, a.BorderEdges.Count, "outer 6 + both sides of the seam");
            Assert.AreEqual(7, a.SeamEdges3D.Count, "outer 6 + seam 1");
        }

        [Test]
        public void SubmeshBoundary_SplitsIslandsAndParts()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: false, secondSubmesh: true));
            Assert.AreEqual(2, a.Islands.Count);
            Assert.AreEqual(2, a.Parts.Count);
        }

        [Test]
        public void Cube_SixIslandsOnePart()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.Cube());
            Assert.AreEqual(6, a.Islands.Count);
            Assert.AreEqual(1, a.Parts.Count);
            Assert.AreEqual(12, a.SeamEdges3D.Count);
            Assert.AreEqual(24, a.BorderEdges.Count);
            Assert.AreEqual(36, a.AdjTris.Length);
            for (int t = 0; t < a.TriangleCount; t++)
                Assert.AreEqual(3, a.AdjStart[t + 1] - a.AdjStart[t]);
        }

        [Test]
        public void FarFromOrigin_StillWelds()
        {
            var input = TestMeshes.TwoQuads(separateUV: false);
            for (int i = 0; i < input.Positions.Length; i++) input.Positions[i] += new Vector3(1e6f, -1e6f, 5e5f);
            var a = MeshAnalyzer.Analyze(input);
            Assert.AreEqual(1, a.Islands.Count);
            Assert.AreEqual(1, a.Parts.Count);
        }

        [Test]
        public void GroupTable_IsConsistentCsr()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.Cube());
            var g = a.Islands;
            Assert.AreEqual(a.TriangleCount, g.GroupStart[g.Count]);
            for (int k = 0; k < g.Count; k++)
                for (int i = g.GroupStart[k]; i < g.GroupStart[k + 1]; i++)
                    Assert.AreEqual(k, g.TriToGroup[g.GroupTris[i]]);
        }

        [Test]
        public void Outline_DependsOnGranularity()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: true));
            Assert.AreEqual(4, Grouping.Outline(a, SelectionGranularity.UVIsland, 0).Count);
            Assert.AreEqual(6, Grouping.Outline(a, SelectionGranularity.ConnectedMesh, 0).Count);
            Assert.AreEqual(3, Grouping.Outline(a, SelectionGranularity.Polygon, 0).Count);
        }

        [Test]
        public void MeshHash_DependsOnIndicesOnly()
        {
            var a = MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: false));
            var b = MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: true));
            Assert.AreEqual(a.MeshHash, b.MeshHash, "same triangles, different UV");
            var c = TestMeshes.TwoQuads(separateUV: false);
            int tmp = c.Indices[0];
            c.Indices[0] = c.Indices[1];
            c.Indices[1] = tmp;
            Assert.AreNotEqual(a.MeshHash, MeshAnalyzer.Analyze(c).MeshHash);
        }
    }

    public class UVWrapTests
    {
        [Test]
        public void ShiftsTriangleIntoUnitTile()
        {
            var uv = new[] { new Vector2(1.1f, 2.1f), new Vector2(1.3f, 2.1f), new Vector2(1.1f, 2.3f) };
            var tri = UVWrap.Apply(uv, new[] { 0, 1, 2 }, out bool wrap);
            Assert.IsFalse(wrap);
            Assert.AreEqual(0.1f, tri[0].x, 1e-5f);
            Assert.AreEqual(0.1f, tri[0].y, 1e-5f);
        }

        [Test]
        public void StraddlingTriangle_NeedsWrap()
        {
            var uv = new[] { new Vector2(0.9f, 0.5f), new Vector2(1.05f, 0.5f), new Vector2(0.9f, 0.6f) };
            UVWrap.Apply(uv, new[] { 0, 1, 2 }, out bool wrap);
            Assert.IsTrue(wrap);
        }
    }

    public class MirrorTests
    {
        [Test]
        public void SymmetricQuads_PairUp()
        {
            var input = TestMeshes.TwoQuads(separateUV: true);
            // left quad x:0..1 -> x:-2..-1 so that it mirrors the right quad x:1..2
            for (int i = 0; i < 4; i++) input.Positions[i] = new Vector3(-input.Positions[i].x - 1f, input.Positions[i].y, 0);
            input.MirrorSpacePositions = input.Positions;
            var a = MeshAnalyzer.Analyze(input);
            Assert.AreEqual(1f, a.MirrorMatchRate, 1e-5f);
            int g0 = a.Islands.TriToGroup[0];
            int g1 = a.Islands.TriToGroup[2];
            Assert.AreEqual(g1, MirrorResolver.Resolve(a.Islands, a.MirrorTri, g0));
            Assert.AreEqual(g0, MirrorResolver.Resolve(a.Islands, a.MirrorTri, g1));
        }

        [Test]
        public void NoCounterpart_ReturnsMinusOne()
        {
            var input = TestMeshes.TwoQuads(separateUV: true);
            for (int i = 0; i < input.Positions.Length; i++) input.Positions[i] += new Vector3(10f, 0, 0);
            input.MirrorSpacePositions = input.Positions;
            var a = MeshAnalyzer.Analyze(input);
            Assert.AreEqual(0f, a.MirrorMatchRate, 1e-5f);
            Assert.AreEqual(-1, MirrorResolver.Resolve(a.Islands, a.MirrorTri, 0));
        }
    }
}
