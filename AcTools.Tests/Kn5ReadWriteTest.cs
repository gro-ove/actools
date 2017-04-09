using System.IO;
using System.Linq;
using AcTools.Kn5File;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class Kn5ReadWriteTest {
        [TestMethod]
        public void Main() {
            var f = @"D:\Games\Assetto Corsa\content\cars\peugeot_504\peugeot_504.kn5";
            f = @"D:\Games\Assetto Corsa\content\cars\ks_porsche_911_carrera_rsr\porsche_911_carrera_rsr.kn5";

            var t = Path.GetTempFileName();
            var kn5 = Kn5.FromFile(f);

            File.Delete(t);
            kn5.Save(t);

            kn5 = Kn5.FromFile(t);
            kn5.Save(t + "2");

            Assert.IsTrue(File.ReadAllBytes(t + "2").SequenceEqual(File.ReadAllBytes(f)), "I admit, this test is hardcore, but it’s not an excuse");
        }
    }
}