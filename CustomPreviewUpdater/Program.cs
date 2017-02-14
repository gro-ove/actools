using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AcTools.Processes;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CommandLine;
using StringBasedFilter;

namespace CustomPreviewUpdater {
    class Program {
        private static PackedHelper _helper;

        [STAThread]
        private static int Main(string[] a) {
            _helper = new PackedHelper("AcTools_PreviewUpdater", "References", null);
            AppDomain.CurrentDomain.AssemblyResolve += _helper.Handler;
            return MainInner(a);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MainInner(string[] args) {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options) || options.Ids.Count == 0) return 1;

            var acRoot = Path.GetFullPath(options.AcRoot);

            IFilter<string> filter;

            try {
                filter = Filter.Create(new CarTester(acRoot), options.Ids.Select(x =>
                        "(" + (x.EndsWith("/") ? x.Substring(0, x.Length - 1) : x) + ")").JoinToString("|"), true);
                if (options.FilterTest) {
                    Console.WriteLine(Directory.GetDirectories(FileUtils.GetCarsDirectory(options.AcRoot))
                                               .Select(Path.GetFileName).Where(x => filter.Test(x)).JoinToString(", "));
                    return 0;
                }

                if (options.Verbose) {
                    Console.WriteLine("Filter: " + filter);
                }
            } catch (Exception e) {
                Console.Error.WriteLine("Can’t parse filter: " + e);
                return 2;
            }

            if (options.Verbose) {
                Console.WriteLine("AC root: " + options.AcRoot);
                Console.WriteLine("Camera position: " + options.CameraPosition);
                Console.WriteLine("Look at: " + options.LookAt);
                Console.WriteLine("FOV: " + options.Fov);
                Console.WriteLine("Starting shoting...");
            }

            var sw = Stopwatch.StartNew();
            int i = 0, j = 0;

            using (var thing = new DarkPreviewsUpdater(acRoot, new DarkPreviewsOptions {
                PreviewName = options.FileName,
                SsaaMultipler = options.SsaaMultipler,
                UseFxaa = options.UseFxaa,
                UseMsaa = options.UseMsaa,
                MsaaSampleCount = options.MsaaSampleCount,
                PreviewWidth = options.PreviewWidth,
                PreviewHeight = options.PreviewHeight,
                BloomRadiusMultipler = options.BloomRadiusMultipler,
                FlatMirror = options.FlatMirror,
                WireframeMode = options.WireframeMode,
                MeshDebugMode = options.MeshDebugMode,
                SuspensionDebugMode = options.SuspensionDebugMode,
                HeadlightsEnabled = options.HeadlightsEnabled,
                BrakeLightsEnabled = options.BrakeLightsEnabled,
                SteerAngle = options.SteerAngle,
                CameraPosition = options.CameraPosition.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray(),
                CameraLookAt = options.LookAt.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray(),
                CameraFov = options.Fov,
                BackgroundColor = ParseColor(options.BackgroundColor),
                LightColor = ParseColor(options.LightColor),
                AmbientUp = ParseColor(options.AmbientUp),
                AmbientDown = ParseColor(options.AmbientDown),
                AmbientBrightness = options.AmbientBrightness,
                LightBrightness = options.LightBrightness,
            })) {
                foreach (var carId in Directory.GetDirectories(FileUtils.GetCarsDirectory(options.AcRoot))
                                            .Select(Path.GetFileName).Where(x => filter.Test(x))) {
                    if (options.Verbose) Console.WriteLine($"  {carId}...");
                    j++;

                    foreach (var skinId in Directory.GetDirectories(FileUtils.GetCarSkinsDirectory(acRoot, carId))
                                                    .Where(x => !options.WithoutPreviews || !File.Exists(Path.Combine(x, options.FileName)))
                                                    .Select(Path.GetFileName)) {
                        var success = false;
                        for (var a = 0; a < options.AttemptsCount || a == 0; a++) {
                            try {
                                if (options.Verbose) Console.Write($"    {skinId}... ");
                                thing.Shot(carId, skinId);
                                success = true;
                                break;
                            } catch (Exception e) {
                                Console.Error.WriteLine(e.Message);
                            }
                        }

                        if (success) {
                            i++;
                            if (options.Verbose) Console.WriteLine("OK");
                        }
                    }

                    if (j % 10 == 0) {
                        Console.WriteLine($"At this moment done: {i} skins ({sw.Elapsed.TotalMilliseconds / j:F1} ms per car; {sw.Elapsed.TotalMilliseconds / i:F1} ms per skin)");
                        Console.WriteLine($"Time taken: {ToMillisecondsString(sw.Elapsed)}");
                    }
                }
            }

            Console.WriteLine($"Done: {i} skins ({sw.Elapsed.TotalMilliseconds / j:F1} ms per car; {sw.Elapsed.TotalMilliseconds / i:F1} ms per skin)");
            Console.WriteLine($"Time taken: {ToMillisecondsString(sw.Elapsed)}");

            return 0;
        }

        private static Color ParseColor(string value) {
            return Color.FromArgb(int.Parse(value.Replace("#", "", StringComparison.Ordinal), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        private static string ToMillisecondsString(TimeSpan span) {
            return span.TotalHours > 1d
                    ? $@"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
                    : $@"{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}
