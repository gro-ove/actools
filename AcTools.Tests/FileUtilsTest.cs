using System.IO;
using System.Reflection;
using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class FileUtilsTest {
        [TestMethod]
        public void GetRelativePath() {
            Assert.AreEqual(
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows"),
                    @"system32\a.exe");

            Assert.AreEqual(
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows\"),
                    @"system32\a.exe");

            Assert.AreEqual(
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:/Windows"),
                    @"system32\a.exe");

            Assert.AreEqual(
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows\s"),
                    @"C:\Windows\system32\a.exe");
        }

        [TestMethod]
        public void EnsureUniqueTest() {
            var testDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))) ?? "", "test");

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