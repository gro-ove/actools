using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using AcTools.AcdFile;
using AcTools.DataFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class DataWrapperTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [TestMethod]
        public void TestPacked() {
            var file = DataWrapper.FromDirectory(Path.Combine(TestDir, "data", "peugeot_504"));
            Assert.AreEqual("VALID_INI_FILE", file.GetRawFile("mirrors.ini").Content);
            Assert.AreEqual("VALID_LUT_FILE", file.GetRawFile("power.lut").Content);

            file = DataWrapper.FromDirectory(Path.Combine(TestDir, "data", "peugeot_504_unpacked"));
            Assert.AreEqual("VALID_INI_FILE", file.GetRawFile("mirrors.ini").Content);
            Assert.AreEqual("VALID_LUT_FILE", file.GetRawFile("power.lut").Content);
        }

        [TestMethod]
        public void TestEnc() {
            var enc = AcdEncryption.FromAcdFilename("anything/actually");
            var bytes = Encoding.UTF8.GetBytes("Long testing string with русскими символами and emojis like 😺");
            var cloned = bytes.ToArray();

            enc.Encrypt(cloned);
            enc.Decrypt(cloned);

            Assert.IsTrue(bytes.SequenceEqual(cloned));
        }
    }
}