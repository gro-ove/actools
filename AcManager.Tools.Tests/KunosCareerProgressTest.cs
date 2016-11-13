using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcManager.Tools.Tests {
    [TestClass]
    public class KunosCareerProgressTest {
        [TestMethod]
        public async Task Test() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcManager.Tools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test", "kunoscareer");

            IniFile.Write(Path.Combine(testDir, "progress_1.ini"), "SERIES1", "EVENT0", 1);
            using (var progress = KunosCareerProgress.CreateForTests(Path.Combine(testDir, "progress_1.ini"))) {
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

                IniFile.Write(Path.Combine(testDir, "progress_1.ini"), "SERIES1", "EVENT0", 3);

                await Task.Delay(500);

                Assert.IsTrue(changed);
                Assert.AreEqual(3, progress.Entries["series1"].EventsResults[0]);
            }

            using (var progress = KunosCareerProgress.CreateForTests(Path.Combine(testDir, "progress_2.ini"))) {
                Assert.AreEqual(94, progress.Entries["series_ruf_cup"].AiPoints[4]);
            }
        }
    }
}