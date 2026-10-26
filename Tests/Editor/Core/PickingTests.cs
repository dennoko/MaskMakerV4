using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dennoko.UVMaskMaker.Tests
{
    public class PickingTests
    {
        [Test]
        public void Raycast_HitsNearest()
        {
            var cube = TestMeshes.Cube();
            var rc = new MeshRaycaster(cube.Indices, cube.Positions);
            Assert.IsTrue(rc.Raycast(new Vector3(0.1f, 0.2f, -5f), Vector3.forward, null, out var hit));
            Assert.AreEqual(4.5f, hit.Distance, 1e-4f);
            Assert.IsFalse(rc.Raycast(new Vector3(3f, 3f, -5f), Vector3.forward, null, out _));
        }

        [Test]
        public void Raycast_CullsFacesLikeTheMaterial()
        {
            var cube = TestMeshes.Cube();
            var rc = new MeshRaycaster(cube.Indices, cube.Positions, cube.TriSubmesh);
            var inside = new Vector3(0.1f, 0.2f, 0f);
            Assert.IsFalse(rc.Raycast(inside, Vector3.forward, null, out _), "default Back: back faces are not hit");

            rc.SubmeshCull = new[] { CullMode.Off };
            Assert.IsTrue(rc.Raycast(inside, Vector3.forward, null, out var hit), "Off: back faces are hit");
            Assert.AreEqual(0.5f, hit.Distance, 1e-4f);

            rc.SubmeshCull = new[] { CullMode.Front };
            Assert.IsTrue(rc.Raycast(new Vector3(0.1f, 0.2f, -5f), Vector3.forward, null, out hit), "Front: only the far side is hit");
            Assert.AreEqual(5.5f, hit.Distance, 1e-4f);
        }

        [Test]
        public void Raycast_PrefersFrontFaceOnCoincidentTriangles()
        {
            // 同じ位置の表裏 2 枚（裏が先）。-Z から +Z を見て (a, b, c) が表
            var pos = new[] { Vector3.zero, Vector3.up, Vector3.right };
            var rc = new MeshRaycaster(new[] { 0, 2, 1, 0, 1, 2 }, pos, new[] { 0, 0 }) { SubmeshCull = new[] { CullMode.Off } };
            Assert.IsTrue(rc.Raycast(new Vector3(0.2f, 0.2f, -1f), Vector3.forward, null, out var hit));
            Assert.AreEqual(1, hit.Triangle);
        }

        [Test]
        public void Raycast_DoesNotLeakThroughSharedEdges()
        {
            var cube = TestMeshes.Cube();
            var rc = new MeshRaycaster(cube.Indices, cube.Positions);
            // -Z 面の対角線（2 三角形の共有辺）と面の外周の頂点
            foreach (var p in new[] { new Vector3(0.1f, 0.1f, -5f), new Vector3(-0.3f, -0.3f, -5f), new Vector3(0.5f, 0.5f, -5f) })
            {
                Assert.IsTrue(rc.Raycast(p, Vector3.forward, null, out var hit), p.ToString());
                Assert.AreEqual(4.5f, hit.Distance, 1e-4f, p.ToString());
            }
        }

        [Test]
        public void Raycast_SkipsInactiveTriangles()
        {
            var cube = TestMeshes.Cube();
            var rc = new MeshRaycaster(cube.Indices, cube.Positions);
            var active = TriangleBits.Create(cube.TriangleCount);
            Assert.IsFalse(rc.Raycast(new Vector3(0.1f, 0.2f, -5f), Vector3.forward, active, out _));
        }

        [Test]
        public void UVGrid_ReturnsAllOverlappingTriangles()
        {
            var uv = new[]
            {
                new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.1f), new Vector2(0.1f, 0.5f),
                new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.1f), new Vector2(0.1f, 0.5f),
                new Vector2(0.7f, 0.7f), new Vector2(0.7001f, 0.7f), new Vector2(0.7f, 0.7001f),
            };
            var grid = new UVGrid(uv);
            var result = new List<int>();
            grid.Query(new Vector2(0.2f, 0.2f), null, result);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result);
            grid.Query(new Vector2(0.70002f, 0.70002f), null, result);
            CollectionAssert.AreEquivalent(new[] { 2 }, result, "tiny triangle is still pickable");
            grid.Query(new Vector2(0.9f, 0.9f), null, result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void UVGrid_WrapsStraddlingTriangles()
        {
            var uv = new[] { new Vector2(0.9f, 0.5f), new Vector2(1.2f, 0.5f), new Vector2(0.9f, 0.7f) };
            var grid = new UVGrid(uv);
            var result = new List<int>();
            grid.Query(new Vector2(0.02f, 0.52f), null, result);
            CollectionAssert.AreEquivalent(new[] { 0 }, result, "the part beyond 1 wraps to 0");
        }
    }
}
