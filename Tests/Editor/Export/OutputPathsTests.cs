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
            Assert.AreEqual(f + "/m_inv 1.png", inv);

            File.WriteAllBytes(f + "/m 1.png", new byte[1]);
            (main, inv) = OutputPaths.ResolveUniqueMaskPaths(f, "m", true);
            Assert.AreEqual(f + "/m 2.png", main, "increments until both main and inv are free");
            Assert.AreEqual(f + "/m_inv 2.png", inv);

            (main, inv) = OutputPaths.ResolveUniqueMaskPaths(f, "m", false);
            Assert.AreEqual(f + "/m.png", main);
            Assert.IsNull(inv);

            File.WriteAllBytes(f + "/m.png", new byte[1]);
            (main, inv) = OutputPaths.ResolveUniqueMaskPaths(f, "m", false);
            Assert.AreEqual(f + "/m 2.png", main);
            Assert.IsNull(inv);
        }

        [Test]
        public void AppendMaskSubfolder_DoesNotNestWhenAlreadyInMask()
        {
            Assert.AreEqual("Assets/Textures/Mask", OutputPaths.AppendMaskSubfolder("Assets/Textures", true));
            Assert.AreEqual("Assets/Textures/Mask", OutputPaths.AppendMaskSubfolder("Assets/Textures/", true));
            Assert.AreEqual("Assets/Textures/Mask", OutputPaths.AppendMaskSubfolder("Assets/Textures/Mask", true));
            Assert.AreEqual("Assets/Textures/mask", OutputPaths.AppendMaskSubfolder("Assets/Textures/mask", true));
            Assert.AreEqual("Assets/Textures", OutputPaths.AppendMaskSubfolder("Assets/Textures", false));
        }
    }
}
