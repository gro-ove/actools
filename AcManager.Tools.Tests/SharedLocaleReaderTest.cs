using System.IO;
using System.Runtime.CompilerServices;
using AcManager.Tools.Miscellaneous;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcManager.Tools.Tests {
    [TestClass]
    public class SharedLocaleReaderTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [TestMethod]
        public void Test() {
            var read = SharedLocaleReader.Read(Path.Combine(TestDir, "test.xlsx"), "ru");
            Assert.AreEqual("Крут. момент:", read["AppStrings"]["CarSpecs_TorqueLabel"]);
            Assert.IsFalse(read["AppStrings"].ContainsKey("Settings_Locale_LoadCustom"));

            var nothing = SharedLocaleReader.Read(Path.Combine(TestDir, "test.xlsx"), "pl");
            Assert.AreEqual(0, nothing.Count);
        }
    }
}