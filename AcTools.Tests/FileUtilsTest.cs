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
    }
}