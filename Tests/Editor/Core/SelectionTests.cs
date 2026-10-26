using System.Collections.Generic;
using Dennoko.UVMaskMaker.Core;
using NUnit.Framework;

namespace Dennoko.UVMaskMaker.Tests
{
    public class SelectionTests
    {
        private static MeshAnalysis Quads(bool secondSubmesh = false)
            => MeshAnalyzer.Analyze(TestMeshes.TwoQuads(separateUV: true, secondSubmesh: secondSubmesh));

        [Test]
        public void SmartToggle_AddsPartialAndRemovesFull()
        {
            var a = Quads();
            var bits = TriangleBits.Create(a.TriangleCount);
            var groups = new List<int> { a.Islands.TriToGroup[0] };
            Assert.IsTrue(SelectionOps.ToggleResult(bits, a.Islands, groups), "None -> add");
            TriangleBits.Set(bits, 0, true);
            Assert.AreEqual(GroupSelection.Partial, SelectionOps.StateOf(bits, a.Islands, groups[0]));
            Assert.IsTrue(SelectionOps.ToggleResult(bits, a.Islands, groups), "Partial -> add");
            SelectionOps.SetGroup(bits, a.Islands, groups[0], true);
            Assert.AreEqual(GroupSelection.Full, SelectionOps.StateOf(bits, a.Islands, groups[0]));
            Assert.IsFalse(SelectionOps.ToggleResult(bits, a.Islands, groups), "Full -> remove");
        }

        [Test]
        public void SwitchingGranularity_KeepsTriangles()
        {
            var a = Quads();
            var bits = TriangleBits.Create(a.TriangleCount);
            SelectionOps.SetGroup(bits, a.Polygons, 1, true);
            Assert.AreEqual(GroupSelection.Partial, SelectionOps.StateOf(bits, a.Islands, a.Islands.TriToGroup[1]));
            Assert.AreEqual(1, TriangleBits.Count(bits));
        }

        [Test]
        public void Invert_TouchesOnlyActiveSubmesh()
        {
            var a = Quads(secondSubmesh: true);
            var bits = TriangleBits.Create(a.TriangleCount);
            var active = TriangleBits.ActiveMask(a.TriSubmesh, 0);
            SelectionOps.Invert(bits, active);
            Assert.IsTrue(TriangleBits.Get(bits, 0));
            Assert.IsTrue(TriangleBits.Get(bits, 1));
            Assert.IsFalse(TriangleBits.Get(bits, 2));
            Assert.IsFalse(TriangleBits.Get(bits, 3));
        }

        [Test]
        public void GrowAndShrink_OneStep()
        {
            var a = Quads();
            var bits = TriangleBits.Create(a.TriangleCount);
            var active = TriangleBits.ActiveMask(a.TriSubmesh, -1);
            SelectionOps.SetGroup(bits, a.Islands, a.Islands.TriToGroup[0], true);
            Assert.IsTrue(SelectionOps.Grow(bits, active, a.Islands, a.AdjStart, a.AdjTris));
            Assert.AreEqual(4, TriangleBits.Count(bits), "grows across the seam");
            Assert.IsFalse(SelectionOps.Grow(bits, active, a.Islands, a.AdjStart, a.AdjTris));
            Assert.IsFalse(SelectionOps.Shrink(bits, active, a.Islands, a.AdjStart, a.AdjTris), "open boundary has no neighbours");
            TriangleBits.Set(bits, 3, false);
            Assert.IsTrue(SelectionOps.Shrink(bits, active, a.Islands, a.AdjStart, a.AdjTris));
        }

        [Test]
        public void CountGroups_CountsFullOnly()
        {
            var a = Quads();
            var bits = TriangleBits.Create(a.TriangleCount);
            var active = TriangleBits.ActiveMask(a.TriSubmesh, -1);
            TriangleBits.Set(bits, 0, true);
            SelectionOps.CountGroups(bits, active, a.Islands, out int full, out int total);
            Assert.AreEqual(0, full);
            Assert.AreEqual(2, total);
            SelectionOps.SelectAll(bits, active);
            SelectionOps.CountGroups(bits, active, a.Islands, out full, out total);
            Assert.AreEqual(2, full);
        }

        [Test]
        public void Bits_SetGetCountAcrossWords()
        {
            var bits = TriangleBits.Create(100);
            Assert.AreEqual(4, bits.Length);
            TriangleBits.Set(bits, 31, true);
            TriangleBits.Set(bits, 32, true);
            TriangleBits.Set(bits, 99, true);
            Assert.IsTrue(TriangleBits.Get(bits, 31));
            Assert.IsTrue(TriangleBits.Get(bits, 32));
            Assert.AreEqual(3, TriangleBits.Count(bits));
            TriangleBits.Set(bits, 31, false);
            Assert.AreEqual(2, TriangleBits.Count(bits));
        }
    }
}
