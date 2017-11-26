using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimDX;

namespace AcTools.Render.Tests {
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
                Kn5 = AcPaths.GetMainCarFilename(x)
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
                    renderer.SetCamera(new Vector3(3.867643f, 1.42359f, 4.70381f), new Vector3(0.0f, 0.7f, 0.5f), (float)(Math.PI / 180d * 30f), 0f);
                    renderer.BackgroundColor = Color.FromArgb(220, 220, 220);

                    renderer.Initialize();
                    renderer.Width = CommonAcConsts.PreviewWidth;
                    renderer.Height = CommonAcConsts.PreviewHeight;
                    renderer.ResolutionMultiplier = 1d;

                    do {
                        if (e.Current != first) {
                            first = e.Current;
                            if (first == null) return;

                            await renderer.MainSlot.SetCarAsync(new CarDescription(first.Kn5, first.CarDirectory));
                        }

                        Console.WriteLine(first.CarId);

                        foreach (var skinDirectory in Directory.GetDirectories(Path.Combine(first.CarDirectory, "skins"))) {
                            using (var stream = new MemoryStream()) {
                                renderer.Shot(renderer.Width * 4, renderer.Height * 4, 1d, 1d, stream, RendererShotFormat.Png);
                                Image.FromStream(stream).HighQualityResize(new Size(CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight))
                                     .Save(Path.Combine(skinDirectory, "preview_new.jpg"));
                            }

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