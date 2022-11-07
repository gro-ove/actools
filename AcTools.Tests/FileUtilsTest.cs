using System.IO;
using System.Runtime.CompilerServices;
using AcTools.Utils;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class FileUtilsTest {
        [Test]
        public void GetRelativePath() {
            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\WiNDows\system32\a.exe", @"C:\Windows\"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:/WinDOws"));

            Assert.AreEqual(@"..\system32\a.exe",
                    FileUtils.GetRelativePath(@"C:\Windows\system32\a.exe", @"C:\Windows\s"));

            Assert.AreEqual(@"..\Windows",
                    FileUtils.GetRelativePath(@"C:\Windows", @"C:\Windows"));
        }
        
        [Test]
        public void ReplaceExtension() {
            Assert.AreEqual(@"C:\Windows\system32\a.bin",
                    FileUtils.ReplaceExtension(@"C:\Windows\system32\a.exe", @".bin"));
            
            Assert.AreEqual(@"C:\Windows\system32/a.bin",
                    FileUtils.ReplaceExtension(@"C:\Windows\system32/a.exe", @".bin"));
            
            Assert.AreEqual(@"C:\Windows\system32/a_exe.bin",
                    FileUtils.ReplaceExtension(@"C:\Windows\system32/a_exe", @".bin"));
            
            Assert.AreEqual(@"C:\Windows\system.32/a_exe.bin",
                    FileUtils.ReplaceExtension(@"C:\Windows\system.32/a_exe", @".bin"));
            
            Assert.AreEqual(@"C:\Windows\system32\a",
                    FileUtils.ReplaceExtension(@"C:\Windows\system32\a.exe", @""));
            
            Assert.AreEqual(@"a.bin",
                    FileUtils.ReplaceExtension(@"a.exe", @".bin"));
            
            Assert.AreEqual(@"a_exe.bin",
                    FileUtils.ReplaceExtension(@"a_exe", @".bin"));
            
            Assert.AreEqual(@"a",
                    FileUtils.ReplaceExtension(@"a.exe", @""));
            
            Assert.AreEqual(@".bin",
                    FileUtils.ReplaceExtension(@".exe", @".bin"));
            
            Assert.AreEqual(@"",
                    FileUtils.ReplaceExtension(@".exe", @""));
        }

        [Test]
        public void GetPathWithin() {
            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetPathWithin(@"C:\Windows\system32\a.exe", @"C:\Windows"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetPathWithin(@"C:\WiNDows\system32\a.exe", @"C:\Windows\"));

            Assert.AreEqual(@"system32\a.exe",
                    FileUtils.GetPathWithin(@"C:\Windows\system32\a.exe", @"C:/WinDOws"));

            Assert.AreEqual(null,
                    FileUtils.GetPathWithin(@"C:\Windows\system32\a.exe", @"C:\Windows\s"));

            Assert.AreEqual(@"",
                    FileUtils.GetPathWithin(@"C:\Windows", @"C:\Windows"));
        }

        [Test]
        public void NormalizePath() {
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\Windows"));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\Windows\"));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\/Windows\\"));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\/Windows\\system32\.."));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@"C:\.\Windows\."));
            Assert.AreEqual(@"C:\Windows", FileUtils.NormalizePath(@".\C:\.\Windows\..\Windows"));
        }

        [Test]
        public void IsAffected() {
            Assert.IsTrue(FileUtils.IsAffectedBy(@"C:\Windows\system32\a.exe", @"C:\Windows"));
            Assert.IsTrue(FileUtils.IsAffectedBy(@"C:\WiNDows\system32\a.exe", @"C:\Windows\"));
            Assert.IsTrue(FileUtils.IsAffectedBy(@"C:\Windows\system32\a.exe", @"C:/WinDOws"));
            Assert.IsFalse(FileUtils.IsAffectedBy(@"C:\Windows\system32\a.exe", @"C:\Windows\s"));
            Assert.IsTrue(FileUtils.IsAffectedBy(@"C:\Windows", @"C:\Windows"));
            Assert.IsTrue(FileUtils.IsAffectedBy(@"c:/windows/system32", @"C:\Windows"));
            Assert.IsFalse(FileUtils.IsAffectedBy(@"c:/wind", @"C:\Windows"));
            Assert.IsFalse(FileUtils.IsAffectedBy(@"c:/windows/system32", @"C:\Windows2"));
            Assert.IsFalse(FileUtils.IsAffectedBy(@"c:/windows/system32/abc", @"C:\Windows\qwerty"));
            Assert.IsFalse(FileUtils.IsAffectedBy(@"williams_fw24\content\cars\msf_williams_fw24\CAR_SUSP_LF.ksanim", @"williams_fw24\content\driver\driver_fw24.kn5"));
        }

        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [Test]
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