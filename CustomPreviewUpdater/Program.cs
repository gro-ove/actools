using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CommandLine;
using StringBasedFilter;

namespace CustomPreviewUpdater {
    public static class Program {
        private static PackedHelper _helper;

#if PLATFORM_X86
        private static readonly string Platform = "x86";
#else
        private static readonly string Platform = "x64";
#endif

        [STAThread]
        private static int Main(string[] a) {
            _helper = new PackedHelper("AcTools_PreviewUpdater", "References", null);
            _helper.PrepareUnmanaged($"Magick.NET-Q8-{Platform}.Native");
            AppDomain.CurrentDomain.AssemblyResolve += _helper.Handler;

            try {
                return MainInner(a);
            } catch (Exception e) {
                Console.Error.WriteLine("Fatal error: " + e.Message + ".");
                Console.Error.WriteLine(e.StackTrace);
                Console.ReadLine();
                return 10;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MainInner(string[] args) {
            var presets = args.Where(x => x.EndsWith(".pu-preset")).ToList();
            var actualList = new List<string>();

            foreach (var preset in presets) {
                try {
                    actualList.AddRange(
                            File.ReadAllLines(preset)
                                .Where(x => !x.StartsWith("#"))
                                .Select(x => x.Split(new[] { " #" }, StringSplitOptions.None)[0].Trim())
                                .Where(x => x.Length > 0));
                } catch (Exception e) {
                    Console.Error.WriteLine($"Can't load preset {preset}: {e.Message}.");
                }
            }

            actualList.AddRange(args.ApartFrom(presets));

            var options = new Options();
            if (!Parser.Default.ParseArguments(actualList.ToArray(), options)) return 1;

            if (options.ColorGradingFilename != null && presets.Count > 0 && !File.Exists(options.ColorGradingFilename)) {
                var locations = presets.Select(Path.GetDirectoryName).ToList();
                var current = Environment.CurrentDirectory;
                foreach (var location in locations) {
                    Environment.CurrentDirectory = location;
                    var path = Path.GetFullPath(options.ColorGradingFilename);
                    if (File.Exists(path)) {
                        options.ColorGradingFilename = path;
                    }
                }

                Environment.CurrentDirectory = current;
            }

            var acRoot = options.AcRoot == null ? AcRootFinder.TryToFind() : Path.GetFullPath(options.AcRoot);
            if (acRoot == null) {
                Console.Error.WriteLine("Can't find AC root directory, you need to specify it manually.");
                Console.ReadLine();
                return 1;
            }

            var ids = options.Ids.ApartFrom(presets).ToList();
            if (ids.Count == 0) {
                Console.Error.WriteLine("You forgot to specify what cars to update: either list their IDs or filters.");
                Console.Error.WriteLine("To process all cars, use filter \"*\".");
                Console.ReadLine();
                return 1;
            }

            IFilter<string> filter;

            try {
                filter = Filter.Create(new CarTester(acRoot), ids.Select(x =>
                        "(" + (x.EndsWith("/") ? x.Substring(0, x.Length - 1) : x) + ")").JoinToString("|"), true);
                if (options.FilterTest) {
                    Console.WriteLine(Directory.GetDirectories(FileUtils.GetCarsDirectory(acRoot))
                                               .Select(Path.GetFileName).Where(x => filter.Test(x)).JoinToString(", "));
                    return 0;
                }

                if (options.Verbose) {
                    Console.WriteLine("Filter: " + filter);
                }
            } catch (Exception e) {
                Console.Error.WriteLine("Can't parse filter: " + e.Message + ".");
                Console.ReadLine();
                return 2;
            }

            if (options.Verbose) {
                Console.WriteLine("AC root: " + acRoot);
                Console.WriteLine("ImageMagick: " + ImageUtils.IsMagickSupported);
                Console.WriteLine("Starting shoting...");
            }

            var sw = Stopwatch.StartNew();
            int i = 0, j = 0;

            using (var thing = new DarkPreviewsUpdater(acRoot, new DarkPreviewsOptions {
                PreviewName = options.FileName,
                Showroom = options.Showroom,
                AlignCar = options.AlignCar,
                AlignCamera = options.AlignCamera,
                AlignCameraOffset = options.AlignCameraOffset.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray(),
                SsaaMultiplier = options.SsaaMultiplier,
                UseFxaa = options.UseFxaa,
                UseMsaa = options.UseMsaa,
                HardwareDownscale = !options.SoftwareDownscale,
                MsaaSampleCount = options.MsaaSampleCount,
                PreviewWidth = options.PreviewWidth,
                PreviewHeight = options.PreviewHeight,
                BloomRadiusMultiplier = options.BloomRadiusMultiplier,
                FlatMirror = options.FlatMirror,
                WireframeMode = options.WireframeMode,
                MeshDebugMode = options.MeshDebugMode,
                SuspensionDebugMode = options.SuspensionDebugMode,
                HeadlightsEnabled = options.HeadlightsEnabled,
                BrakeLightsEnabled = options.BrakeLightsEnabled,
                LeftDoorOpen = options.LeftDoorOpen,
                RightDoorOpen = options.RightDoorOpen,
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
                DelayedConvertation = !options.SingleThread,
                UseSslr = options.UseSslr,
                UseSsao = options.UseSsao,
                UsePcss = options.UsePcss,
                EnableShadows = options.EnableShadows,
                ShadowMapSize = options.ShadowMapSize,
                ReflectionMultiplier = options.ReflectionMultiplier,
                ReflectionCubemapAtCamera = options.ReflectionCubemapAtCamera,
                NoShadowsWithReflections = options.NoShadowsWithReflections,
                FlatMirrorBlurred = options.FlatMirrorBlurred,
                FlatMirrorReflectiveness = options.FlatMirrorReflectiveness,
                LightDirection = options.LightDirection.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray(),
            })) {
                foreach (var carId in Directory.GetDirectories(FileUtils.GetCarsDirectory(acRoot))
                                            .Select(Path.GetFileName).Where(x => filter.Test(x))) {
                    Console.WriteLine($"  {carId}...");
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

                                if (options.Verbose) {
                                    Console.Error.WriteLine(e.StackTrace);
                                }
                            }
                        }

                        if (success) {
                            i++;
                            if (options.Verbose) Console.WriteLine("OK");
                        }
                    }

                    if (options.Verbose && j % 10 == 0) {
                        Console.WriteLine(
                                $"At this moment done: {i} skins ({sw.Elapsed.TotalMilliseconds / j:F1} ms per car; {sw.Elapsed.TotalMilliseconds / i:F1} ms per skin)");
                        Console.WriteLine($"Time taken: {ToMillisecondsString(sw.Elapsed)}");
                    }
                }

                Console.Write("Finishing convertation... ");
            }

            Console.WriteLine("OK");
            Console.WriteLine($"Done: {i} skins ({sw.Elapsed.TotalMilliseconds / j:F1} ms per car; {sw.Elapsed.TotalMilliseconds / i:F1} ms per skin)");
            Console.WriteLine($"Time taken: {ToMillisecondsString(sw.Elapsed)}");

            return 0;
        }

        private static Color ParseColor(string value) {
            return Color.FromArgb(int.Parse(value.Replace("#", "", StringComparison.Ordinal), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        private static string ToMillisecondsString(TimeSpan span) {
            return span.TotalHours > 1d
                    ? $@"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds:D3}"
                    : $@"{span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds:D3}";
        }
    }
}
