using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using AcTools.Kn5File;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class Kn5ExportTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");
        private static string TestDir => GetTestDir();

        private string LoadColladaData(string filename) {
            using (var stream = File.OpenRead(filename)) {
                var xml = XDocument.Load(stream);
                var ns = xml.Root?.GetDefaultNamespace() ?? string.Empty;
                xml.Descendants(ns + "asset").ToList().ForEach(x => x.Remove());
                return xml.ToString(SaveOptions.DisableFormatting);
            }
        }

        [Test]
        public void ExportBonesColladaTest() {
            var kn5 = Kn5.FromFile(TestDir + "/kn5/bones.kn5");
            kn5.ExportCollada(TestDir + "/kn5/bones-out.dae");
            Assert.AreEqual(LoadColladaData(TestDir + "/kn5/bones-ref.dae"), LoadColladaData(TestDir + "/kn5/bones-out.dae"));
        }

        [Test]
        public void ExportMultiplyMaterialsColladaTest() {
            var kn5 = Kn5.FromFile(TestDir + "/kn5/multiply_materials.kn5");
            kn5.ExportCollada(TestDir + "/kn5/multiply_materials-out.dae");
            Assert.AreEqual(LoadColladaData(TestDir + "/kn5/multiply_materials-ref.dae"), LoadColladaData(TestDir + "/kn5/multiply_materials-out.dae"));
        }
    }
}