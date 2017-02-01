using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [TestMethod]
        public void EnsureUniqueTest() {
            var a = Path.Combine(TestDir, "a");
            Assert.AreEqual(a, FileUtils.EnsureUnique(a));

            var b = Path.Combine(TestDir, "b");
            Assert.AreEqual(Path.Combine(TestDir, "b-1"), FileUtils.EnsureUnique(b));

            var bt = Path.Combine(TestDir, "b.txt");
            Assert.AreEqual(Path.Combine(TestDir, "b-1.txt"), FileUtils.EnsureUnique(bt));

            var c = Path.Combine(TestDir, "c");
            Assert.AreEqual(Path.Combine(TestDir, "c-3"), FileUtils.EnsureUnique(c));

            var ct = Path.Combine(TestDir, "c.txt");
            Assert.AreEqual(Path.Combine(TestDir, "c-2.txt"), FileUtils.EnsureUnique(ct));
        }
    }
}