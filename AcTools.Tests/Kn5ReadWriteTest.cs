using System.IO;
using System.Linq;
using AcTools.Kn5File;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class Kn5ReadWriteTest {
        [Test]
        public void SkipTexturesTest() {
            var f = @"D:\Games\Assetto Corsa\content\cars\peugeot_504\peugeot_504.kn5";
            f = @"D:\Games\Assetto Corsa\content\cars\ks_porsche_911_carrera_rsr\porsche_911_carrera_rsr.kn5";

            var normal = Kn5.FromFile(f);
            var withoutTexture = Kn5.FromFile(f, SkippingTextureLoader.Instance);
            Assert.IsTrue(normal.Nodes.Select(x => x.Name).SequenceEqual(withoutTexture.Nodes.Select(x => x.Name)));
        }

        [Test]
        public void Main() {
            var f = @"D:\Games\Assetto Corsa\content\cars\peugeot_504\peugeot_504.kn5";
            f = @"D:\Games\Assetto Corsa\content\cars\ks_porsche_911_carrera_rsr\porsche_911_carrera_rsr.kn5";

            var t = Path.GetTempFileName();
            var kn5 = Kn5.FromFile(f);

            File.Delete(t);
            kn5.SaveNew(t);

            kn5 = Kn5.FromFile(t);
            kn5.SaveNew(t + "2");

            Assert.IsTrue(File.ReadAllBytes(t + "2").SequenceEqual(File.ReadAllBytes(f)), "I admit, this test is hardcore, but it’s not an excuse");
        }
    }
}