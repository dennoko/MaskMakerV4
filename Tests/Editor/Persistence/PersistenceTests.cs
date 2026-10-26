using System.IO;
using NUnit.Framework;

namespace Dennoko.UVMaskMaker.Tests
{
    public class PersistenceTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "UVMaskMakerTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void SessionFile_RoundTrips()
        {
            var path = Path.Combine(_dir, "a", "session.bin");
            var data = new SessionFile.Data
            {
                MeshHash = 0x1234_5678_9abc_def0UL,
                TriangleCount = 70,
                SelectedBits = new[] { 1, -1, 7 },
                PaintRle = new byte[] { 5, 3, 2 },
            };
            SessionFile.Write(path, data);
            Assert.IsTrue(SessionFile.TryRead(path, out var back));
            Assert.AreEqual(data.MeshHash, back.MeshHash);
            Assert.AreEqual(data.TriangleCount, back.TriangleCount);
            CollectionAssert.AreEqual(data.SelectedBits, back.SelectedBits);
            CollectionAssert.AreEqual(data.PaintRle, back.PaintRle);

            // 上書きでも壊れない（一時ファイル → 置き換え）
            data.TriangleCount = 71;
            SessionFile.Write(path, data);
            Assert.IsTrue(SessionFile.TryRead(path, out back));
            Assert.AreEqual(71, back.TriangleCount);
        }

        [Test]
        public void SessionFile_RejectsGarbage()
        {
            var path = Path.Combine(_dir, "bad.bin");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.IsFalse(SessionFile.TryRead(path, out _));
            Assert.IsFalse(SessionFile.TryRead(Path.Combine(_dir, "missing.bin"), out _));
        }
    }
}
