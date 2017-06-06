using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcManager.Tools.Tests {
    // TODO: what’s that?
    /*[TestFixture]
    public class KunosCareerProgressTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [Test]
        public async Task Test() {
            IniFile.Write(Path.Combine(TestDir, "progress_1.ini"), "SERIES1", "EVENT0", 1);
            using (var progress = KunosCareerProgress.CreateForTests(Path.Combine(TestDir, "progress_1.ini"))) {
                Assert.IsFalse(progress.IsNew);
                Assert.AreEqual(1, progress.Entries["series1"].EventsResults[0]);
                Assert.AreEqual(2, progress.Entries["series1"].EventsResults[4]);
                Assert.AreEqual(0, progress.Entries["series1"].EventsResults.GetValueOrDefault(3));
                Assert.AreEqual(2, progress.Entries["series5"].EventsResults[0]);
                Assert.AreEqual(1, progress.Entries["series5"].EventsResults[1]);

                Assert.IsTrue(progress.Completed.SequenceEqual(new[] {
                    "series1", "series2", "series3", "series4", "series5"
                }));

                var changed = false;
                progress.PropertyChanged += (sender, args) => {
                    changed = true;
                };

                IniFile.Write(Path.Combine(TestDir, "progress_1.ini"), "SERIES1", "EVENT0", 3);

                await Task.Delay(500);

                Assert.IsTrue(changed);
                Assert.AreEqual(3, progress.Entries["series1"].EventsResults[0]);
            }

            using (var progress = KunosCareerProgress.CreateForTests(Path.Combine(TestDir, "progress_2.ini"))) {
                Assert.AreEqual(94, progress.Entries["series_ruf_cup"].AiPoints[4]);
            }
        }
    }*/
}