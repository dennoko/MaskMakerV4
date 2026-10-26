using Dennoko.UVMaskMaker.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dennoko.UVMaskMaker.Tests
{
    public class ImagingTests
    {
        private static void AssertRoundTrip(byte[] pixels)
        {
            var rle = PaintRle.Encode(pixels);
            var back = new byte[pixels.Length];
            Assert.IsTrue(PaintRle.Decode(rle, back));
            for (int i = 0; i < pixels.Length; i++)
                Assert.AreEqual(pixels[i] != 0 ? 255 : 0, back[i], "index " + i);
        }

        [Test]
        public void Rle_RoundTrips()
        {
            AssertRoundTrip(new byte[1000]);
            var full = new byte[1003];
            for (int i = 0; i < full.Length; i++) full[i] = 255;
            AssertRoundTrip(full);
            var checker = new byte[257];
            for (int i = 0; i < checker.Length; i++) checker[i] = (byte)(i % 2 == 0 ? 255 : 0);
            AssertRoundTrip(checker);
            var rnd = new System.Random(1);
            var random = new byte[4099];
            for (int i = 0; i < random.Length; i++) random[i] = (byte)(rnd.Next(10) < 3 ? 255 : 0);
            AssertRoundTrip(random);
            var blocks = new byte[64 * 64];
            for (int i = 100; i < 2000; i++) blocks[i] = 255;
            AssertRoundTrip(blocks);
        }

        [Test]
        public void Rle_IsDeterministicAndDetectsEmpty()
        {
            var px = new byte[512];
            CollectionAssert.AreEqual(PaintRle.Encode(px), PaintRle.Encode(px));
            Assert.IsTrue(PaintRle.IsEmpty(PaintRle.Encode(px)));
            Assert.IsTrue(PaintRle.IsEmpty(PaintRle.Empty(512)));
            px[3] = 255;
            Assert.IsFalse(PaintRle.IsEmpty(PaintRle.Encode(px)));
        }

        [Test]
        public void Rle_RejectsCorruptData()
        {
            var dst = new byte[10];
            Assert.IsFalse(PaintRle.Decode(new byte[] { 50 }, dst));
        }

        [Test]
        public void PolygonFill_SquareAndEvenOdd()
        {
            var dst = new byte[10 * 10];
            Assert.IsTrue(PolygonFill.Fill(PolygonFill.Rect(new Vector2(2, 2), new Vector2(5, 4)), 10, 10, dst));
            int count = 0;
            for (int i = 0; i < dst.Length; i++) if (dst[i] != 0) count++;
            Assert.AreEqual(3 * 2, count);
            Assert.AreEqual(255, dst[2 * 10 + 2]);
            Assert.AreEqual(0, dst[4 * 10 + 2]);

            // self-intersecting bow tie: the left/right lobes are inside, top/bottom lobes are outside
            var bow = new[] { new Vector2(0, 0), new Vector2(10, 10), new Vector2(10, 0), new Vector2(0, 10) };
            PolygonFill.Fill(bow, 10, 10, dst);
            Assert.AreEqual(255, dst[5 * 10 + 1], "left lobe");
            Assert.AreEqual(0, dst[1 * 10 + 5], "bottom lobe (even-odd)");
        }

        [Test]
        public void MaskEncoder_ClassicAndChannel()
        {
            var mask = new byte[] { 255, 0 };
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }, MaskEncoder.Classic(mask, false));
            Assert.AreEqual(255, MaskEncoder.Classic(mask, true)[0]);

            var baseRgba = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            CollectionAssert.AreEqual(new byte[] { 0, 20, 30, 255, 50, 60, 70, 80 },
                MaskEncoder.Channel(mask, baseRgba, true, false, false, true, false));
            CollectionAssert.AreEqual(new byte[] { 255, 0, 255, 255, 255, 255, 255, 255 },
                MaskEncoder.Channel(mask, null, false, true, false, false, false));
        }

        [Test]
        public void ImportThreshold_UsesLuminanceAndAlpha()
        {
            var rgba = new byte[]
            {
                0, 0, 0, 255,
                200, 200, 200, 255,
                0, 0, 0, 0,
                100, 100, 100, 255,
            };
            var dst = ImportThreshold.Apply(rgba, 128, out int n);
            Assert.AreEqual(2, n);
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, dst);
        }
    }
}
