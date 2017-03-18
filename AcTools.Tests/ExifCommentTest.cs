using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class ExifCommentTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        private static void Test(string t, int iter, Action a) {
            var ia = (int)Math.Sqrt(iter);

            var ee = double.PositiveInfinity;
            for (var j = 0; j < ia; j++) {
                var s = Stopwatch.StartNew();
                for (var i = 0; i < ia; i++) {
                    a();
                }

                var e = s.Elapsed.TotalMilliseconds;
                if (e < ee) ee = e;
            }

            Console.WriteLine("{0}: {2:F3} ms", t, ee, ee / ia);
        }


        [TestMethod]
        public void EnsureUniqueTest() {
            var r = Path.Combine(TestDir, "r.jpg");
            Assert.AreEqual("Comment (р.я.)", ExifComment.Read(r));

            Test("ExifComment.Read()", 10000, () => {
                ExifComment.Read(r);
            });
        }
    }
}