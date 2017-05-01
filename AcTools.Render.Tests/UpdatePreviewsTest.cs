using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimDX;

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

            using (var updater = new DarkPreviewsUpdater(path)) {
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

    [TestClass]
    public class UpdatePreviewsTest {
        [TestMethod]
        public async Task LamborghiniTest() {
            var path = Path.Combine(AcRootFinder.Find(), @"content\cars");
            if (!Directory.Exists(path)) {
                Debug.WriteLine("REQUIRED ASSET IS MISSING, TEST CANNOT BE DONE");
                return;
            }

            var cars = Directory.GetDirectories(path, "ks_lamborghini_*").Select(x => new {
                CarId = Path.GetFileName(x),
                CarDirectory = x,
                Kn5 = FileUtils.GetMainCarFilename(x)
            }).ToList();

            var sw = Stopwatch.StartNew();
            var i = 0;

            using (var e = cars.GetEnumerator()) {
                if (!e.MoveNext()) return;

                var first = e.Current;
                if (first == null) return;

                using (var renderer = new DarkKn5ObjectRenderer(new CarDescription(first.Kn5, first.CarDirectory))) {
                    renderer.UseMsaa = false;
                    renderer.UseFxaa = false;
                    renderer.AutoRotate = false;
                    renderer.SetCamera(new Vector3(3.867643f, 1.42359f, 4.70381f), new Vector3(0.0f, 0.7f, 0.5f), (float)(Math.PI / 180d * 30f));
                    renderer.BackgroundColor = Color.FromArgb(220, 220, 220);

                    renderer.Initialize();
                    renderer.Width = CommonAcConsts.PreviewWidth;
                    renderer.Height = CommonAcConsts.PreviewHeight;

                    do {
                        if (e.Current != first) {
                            first = e.Current;
                            if (first == null) return;

                            await renderer.SetCarAsync(new CarDescription(first.Kn5, first.CarDirectory));
                        }

                        Console.WriteLine(first.CarId);

                        foreach (var skinDirectory in Directory.GetDirectories(Path.Combine(first.CarDirectory, "skins"))) {
                            // Console.WriteLine(skinDirectory);

                            renderer.Shot(4d, 1d, 1d, true)
                                    .HighQualityResize(new Size(CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight))
                                    .Save(Path.Combine(skinDirectory, "preview_new.jpg"));
                            renderer.SelectNextSkin();
                            i++;
                        }
                    } while (e.MoveNext());
                }
            }

            Console.WriteLine($"Done: {i} skins ({sw.Elapsed.TotalMilliseconds / i:F1} ms per skin)");
        }
    }
}