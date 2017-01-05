using System.IO;
using System.Reflection;
using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class FileUtilsTest {
        [TestMethod]
        public void GetRelativePath() {
            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\WiNDows\system32\a.exe", @"C:\Windows\"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:/WinDOws"));

            Assert.AreEqual(@"C:\Windows\system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows\s"));

            Assert.AreEqual(@"",
                    FileUtils.GetRelativePath(@"C:\Windows", @"C:\Windows"));
        }

        [TestMethod]
        public void EnsureUniqueTest() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcTools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var a = Path.Combine(testDir, "a");
            Assert.AreEqual(a, FileUtils.EnsureUnique(a));

            var b = Path.Combine(testDir, "b");
            Assert.AreEqual(Path.Combine(testDir, "b-1"), FileUtils.EnsureUnique(b));

            var bt = Path.Combine(testDir, "b.txt");
            Assert.AreEqual(Path.Combine(testDir, "b-1.txt"), FileUtils.EnsureUnique(bt));

            var c = Path.Combine(testDir, "c");
            Assert.AreEqual(Path.Combine(testDir, "c-3"), FileUtils.EnsureUnique(c));

            var ct = Path.Combine(testDir, "c.txt");
            Assert.AreEqual(Path.Combine(testDir, "c-2.txt"), FileUtils.EnsureUnique(ct));
        }
    }
}