using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AcManager.Tools.Data;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcManager.Tools.Tests {
    [TestClass]
    public class SharedLocaleReaderTest {
        [TestMethod]
        public void Test() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcManager.Tools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var read = SharedLocaleReader.Read(Path.Combine(testDir, "test.xlsx"), "ru");
            Assert.AreEqual("Крут. момент:", read["AppStrings"]["CarSpecs_TorqueLabel"]);
            Assert.IsFalse(read["AppStrings"].ContainsKey("Settings_Locale_LoadCustom"));

            var nothing = SharedLocaleReader.Read(Path.Combine(testDir, "test.xlsx"), "pl");
            Assert.AreEqual(0, nothing.Count);
        }
    }
}