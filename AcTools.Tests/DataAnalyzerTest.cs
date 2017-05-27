using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.DataAnalyzer;
using AcTools.DataFile;
using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class DataAnalyzerTest {
        private static readonly string AcRoot = AcRootFinder.TryToFind();

        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");

        private static string TestDir => GetTestDir();

        [TestMethod]
        public void Wrapper() {
            var rulesSet = Path.Combine(TestDir, "analyzer", "rules.txt");
            var storage = Path.Combine(TestDir, "analyzer", "storage.data");
            var ids = AcKunosContent.GetKunosCarIds(AcRoot).ToArray();

            var wrapper = RulesWrapper.FromFile(AcRoot, rulesSet, storage, ids);
            wrapper.EnsureActual();

            var d = ids.Where(x => Directory.Exists(FileUtils.GetCarDirectory(AcRoot, x)))
                       .Select(x => new { Id = x, Data = DataWrapper.FromCarDirectory(AcRoot, x) })
                       .ToList();

            var w = Stopwatch.StartNew();
            var j = 0;
            
            foreach (var car in d) {
                foreach (var s in wrapper.FindSimular(car.Data, "aero", false, 0.85)) {
                    AcToolsLogging.Write($"{car.Id} is similar to {s.CarId} by {s.Value * 100:F1}%");
                    j++;
                }
            }

            AcToolsLogging.Write($"Check time: {w.Elapsed.TotalMilliseconds / j:F2} ms");
        }
    }
}