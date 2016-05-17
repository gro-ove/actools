using System.IO;
using System.Reflection;
using AcTools.Kn5File;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class Kn5ExportTest {
        [TestMethod]
        public void ExportBonesColladaTest() {
            var testDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))) ?? "", "test");

            var kn5 = Kn5.FromFile(testDir + "/kn5/bones.kn5");
            kn5.ExportCollada(testDir + "/kn5/bones-out.dae");
        }

        [TestMethod]
        public void ExportMultiplyMaterialsColladaTest() {
            var testDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))) ?? "", "test");

            var kn5 = Kn5.FromFile(testDir + "/kn5/multiply_materials.kn5");
            kn5.ExportCollada(testDir + "/kn5/multiply_materials-out.dae");
        }
    }
}