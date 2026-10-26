using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Tests
{
    public class PickingTests
    {
        [Test]
        public void Raycast_HitsNearestAndBackFaces()
        {
            var cube = TestMeshes.Cube();
            var rc = new MeshRaycaster(cube.Indices, cube.Positions);
            Assert.IsTrue(rc.Raycast(new Vector3(0.1f, 0.2f, -5f), Vector3.forward, null, out var hit));
            Assert.AreEqual(4.5f, hit.Distance, 1e-4f);
            Assert.IsTrue(rc.Raycast(new Vector3(0.1f, 0.2f, 0f), Vector3.forward, null, out hit), "from inside (back face)");
            Assert.AreEqual(0.5f, hit.Distance, 1e-4f);
            Assert.IsFalse(rc.Raycast(new Vector3(3f, 3f, -5f), Vector3.forward, null, out _));
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
