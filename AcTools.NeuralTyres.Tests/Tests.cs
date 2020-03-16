using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using AcTools.DataFile;
using AcTools.NeuralTyres.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;

// ReSharper disable RedundantAssignment
// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace AcTools.NeuralTyres.Tests {
    public class Tests {
        private void BuildChart(params Action<Series>[] series) {
            using (var ch = new Chart()) {
                ch.ChartAreas.Add(new ChartArea());

                foreach (var se in series) {
                    var s = new Series { ChartType = SeriesChartType.Line };
                    se(s);
                    ch.Series.Add(s);
                }

                ch.SaveImage("U:\\nt-test.png", ChartImageFormat.Png);
                Process.Start("U:\\nt-test.png");
            }
        }

        public void Main() {
            var carsFromWebApp = new[] {
                /*"lotus_exige_s", "lotus_exige_s", "lotus_evora_gte", "lotus_elise_sc", "alfa_mito_qv",
                "abarth500", "lotus_elise_sc", "alfa_romeo_giulietta_qv", "bmw_m3_e30", "audi_sport_quattro",
                "audi_a1s1", "nissan_skyline_r34", "ford_mustang_2015", "bmw_1m", "bmw_m4", "bmw_m4",
                "porsche_991_carrera_s", "porsche_718_boxster_s", "porsche_718_boxster_s", "corvette_c7_stingray",
                "porsche_991_carrera_s"*/

                "ks_abarth_595ss_s2", "ks_audi_r8_plus"
            };

            var tyres = Directory.GetDirectories(@"D:\Games\Assetto Corsa\content\cars", "*")
                                 .Where(x => carsFromWebApp.ArrayContains(Path.GetFileName(x)))
                                 .Select(DataWrapper.FromCarDirectory).SelectMany(NeuralTyresEntry.Get)
                                 .Where(x => x.Version == 10).ToList();
            var filteredTyres = tyres.Where(x => x.Name == "Street").ToList();
            // filteredTyres.OrderBy(x => x.Values.GetDouble("RADIUS", 0d)).Select(x => $"{x.Values.GetDouble("RADIUS", 0d)}={x.Values.GetDouble(TestKey, 0d)}").JoinToString("; ").Dump();

            var options = new NeuralTyresOptions {
                OverrideOutputKeys = new[]{ "ANGULAR_INERTIA" }
            };
            var checksum = (filteredTyres.GetEnumerableHashCode() * 397) ^ options.GetHashCode();
            var cacheFilename = Path.Combine("U:\\nt-cache", BitConverter.GetBytes(checksum).ToHexString().ToLowerInvariant() + ".zip");
            Console.WriteLine(checksum);

            TyresMachine neural;
            if (File.Exists(cacheFilename)) {
                neural = TyresMachine.LoadFrom(cacheFilename, null);
            } else {
                neural = TyresMachine.CreateAsync(filteredTyres, options).Result;
                FileUtils.EnsureFileDirectoryExists(cacheFilename);
                // neural?.Save(cacheFilename, null);
            }

            if (neural == null) return;
            var width = neural.GetNormalization(NeuralTyresOptions.InputWidth);
            var radius = neural.GetNormalization(NeuralTyresOptions.InputRadius);
            var profile = neural.GetNormalization(NeuralTyresOptions.InputProfile);

            var testKey = "ANGULAR_INERTIA";
            try {
                BuildChart(Enumerable.Range(0, 7).Select(x => {
                    return (Action<Series>)(s => {
                        for (var i = 0d; i <= 1d; i += 0.01) {
                            var w = width.Denormalize(x / 6d);
                            var r = radius.Denormalize(i);
                            var u = profile.Denormalize(i);
                            s.Points.Add(new DataPoint(r, neural.Conjure(w, r, u)[testKey]));
                            // s.Points.Add(new DataPoint(r, neural.Conjure(testKey, w, r, u)));
                        }

                        s.Color = new[] { Color.Red, Color.Orange, Color.Brown, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta }[x];
                    });
                }).ToArray());
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            Console.WriteLine(neural.Conjure(width.Denormalize(0.5), radius.Denormalize(0.5), profile.Denormalize(0.5))[testKey]);
        }

        public void MainSingleNet() {
            var carsFromWebApp = new[] {
                "lotus_exige_s", "lotus_exige_s", "lotus_evora_gte", "lotus_elise_sc", "alfa_mito_qv",
                "abarth500", "lotus_elise_sc", "alfa_romeo_giulietta_qv", "bmw_m3_e30", "audi_sport_quattro",
                "audi_a1s1", "nissan_skyline_r34", "ford_mustang_2015", "bmw_1m", "bmw_m4", "bmw_m4",
                "porsche_991_carrera_s", "porsche_718_boxster_s", "porsche_718_boxster_s", "corvette_c7_stingray",
                "porsche_991_carrera_s"
            };

            var tyres = Directory.GetDirectories(@"D:\Games\Assetto Corsa\content\cars", "*")
                                 .Where(x => carsFromWebApp.ArrayContains(Path.GetFileName(x)))
                                 .Select(DataWrapper.FromCarDirectory).SelectMany(NeuralTyresEntry.Get)
                                 .Where(x => x.Version == 10).ToList();
            var filteredTyres = tyres.Where(x => x.Name == "Semislicks").ToList();
            // filteredTyres.OrderBy(x => x.Values.GetDouble("RADIUS", 0d)).Select(x => $"{x.Values.GetDouble("RADIUS", 0d)}={x.Values.GetDouble(TestKey, 0d)}").JoinToString("; ").Dump();

            var options = new NeuralTyresOptions {
                AverageAmount = 1,
                SeparateNetworks = false
            };

            var checksum = (filteredTyres.GetEnumerableHashCode() * 397) ^ options.GetHashCode();
            Console.WriteLine($"Options: {options.GetHashCode()}");
            Console.WriteLine($"Filtered tyres: {filteredTyres.GetEnumerableHashCode()}");

            var cacheFilename = Path.Combine("U:\\nt-cache", BitConverter.GetBytes(checksum).ToHexString().ToLowerInvariant() + ".zip");
            Console.WriteLine(cacheFilename);

            TyresMachine neural;
            if (File.Exists(cacheFilename)) {
                neural = TyresMachine.LoadFrom(cacheFilename, null);
            } else {
                neural = TyresMachine.CreateAsync(filteredTyres, options).Result;
                FileUtils.EnsureFileDirectoryExists(cacheFilename);
                neural?.Save(cacheFilename, null);
            }

            if (neural == null) return;
            var width = neural.GetNormalization(NeuralTyresOptions.InputWidth);
            var radius = neural.GetNormalization(NeuralTyresOptions.InputRadius);
            var profile = neural.GetNormalization(NeuralTyresOptions.InputProfile);

            var testKey = "ANGULAR_INERTIA";
            try {
                BuildChart(Enumerable.Range(0, 7).Select(x => {
                    return (Action<Series>)(s => {
                        for (var i = 0d; i <= 1d; i += 0.01) {
                            var w = width.Denormalize(x / 6d);
                            var r = radius.Denormalize(i);
                            var u = profile.Denormalize(i);
                            s.Points.Add(new DataPoint(r, neural.Conjure(w, r, u)[testKey]));
                        }

                        s.Color = new[] { Color.Red, Color.Orange, Color.Brown, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta }[x];
                    });
                }).ToArray());
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            Console.WriteLine(neural.Conjure(width.Denormalize(0.5), radius.Denormalize(0.5), profile.Denormalize(0.5))[testKey]);
        }
    }
}