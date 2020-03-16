using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using AcTools.AcdEncryption;
using AcTools.AcdFile;
using AcTools.DataFile;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class DataWrapperTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [SetUp]
        public void SetUp() {
            Acd.Factory = new AcdFactory();
        }

        [Test]
        public void TestPacked() {
            var file = DataWrapper.FromCarDirectory(Path.Combine(TestDir, "data", "peugeot_504"));
            Assert.AreEqual("VALID_INI_FILE", file.GetRawFile("mirrors.ini").Content);
            Assert.AreEqual("VALID_LUT_FILE", file.GetRawFile("power.lut").Content);

            file = DataWrapper.FromCarDirectory(Path.Combine(TestDir, "data", "peugeot_504_unpacked"));
            Assert.AreEqual("VALID_INI_FILE", file.GetRawFile("mirrors.ini").Content);
            Assert.AreEqual("VALID_LUT_FILE", file.GetRawFile("power.lut").Content);
        }
    }
}