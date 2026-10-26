using System.IO;
using NUnit.Framework;

namespace Dennoko.UVMaskMaker.Tests
{
    public class OutputPathsTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "UVMaskMakerPathTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void UniqueMaskPaths_KeepPairNumbersInSync()
        {
            string f = _dir.Replace('\\', '/');
            var (main, inv) = OutputPaths.ResolveUniqueMaskPaths(f, "m", true);
            Assert.AreEqual(f + "/m.png", main);
            Assert.AreEqual(f + "/m_inv.png", inv);

            File.WriteAllBytes(f + "/m_inv.png", new byte[1]);
            (main, inv) = OutputPaths.ResolveUniqueMaskPaths(f, "m", true);
            Assert.AreEqual(f + "/m 1.png", main, "the inverted file alone also blocks the pair");
            Assert.AreEqual(f + "/m 1_inv.png", inv);

            (main, inv) = OutputPaths.ResolveUniqueMaskPaths(f, "m", false);
            Assert.AreEqual(f + "/m.png", main);
            Assert.IsNull(inv);
        }
    }
}
