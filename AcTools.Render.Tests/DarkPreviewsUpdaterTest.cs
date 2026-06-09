using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Render.Kn5SpecificForwardDark;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Render.Tests {
    [TestClass]
    public class DarkPreviewsUpdaterTest {
        [TestMethod]
        public async Task ATest() {
            var path = AcRootFinder.Find();
            if (!Directory.Exists(path)) {
                Debug.WriteLine("REQUIRED ASSET IS MISSING, TEST CANNOT BE DONE");
                return;
            }

            var cars = Directory.GetDirectories(Path.Combine(path, "content", "cars"), "ks_*").Select(x => new {
                CarId = Path.GetFileName(x),
                SkinsIds = Directory.GetDirectories(Path.Combine(x, "skins")).Select(Path.GetFileName).ToList()
            }).Where(x => Regex.IsMatch(x.CarId, @"^ks_[a]")).ToList();

            var sw = Stopwatch.StartNew();
            var i = 0;

            using (var updater = DarkPreviewsUpdaterFactory.Create(false, path)) {
                foreach (var car in cars) {
                    foreach (var skin in car.SkinsIds) {
                        await updater.ShotAsync(car.CarId, skin);
                        i++;
                    }
                }
            }

            Console.WriteLine($"Done: {i} skins ({sw.Elapsed.TotalMilliseconds / i:F1} ms per skin)");
        }
    }
}