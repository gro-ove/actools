using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.AcdEncryption;
using AcTools.AcdFile;
using AcTools.Kn5File;
using AcTools.Kn5Tools;
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
            Acd.Factory = new AcdFactory();
            Kn5.Factory = Kn5New.GetFactoryInstance();

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
                    Console.WriteLine(Directory.GetDirectories(AcPaths.GetCarsDirectory(acRoot))
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
                Console.WriteLine("Starting making previews...");
            }

            var sw = Stopwatch.StartNew();
            int i = 0, j = 0;

            var previewsOptions = !string.IsNullOrWhiteSpace(options.Preset) ? PresetLoader.Load(options.Preset) : new DarkPreviewsOptions();
            if (string.IsNullOrWhiteSpace(options.Preset)) {
                previewsOptions.PreviewName = options.FileName;
                previewsOptions.Showroom = options.Showroom;
                previewsOptions.AlignCar = options.AlignCar;
                previewsOptions.AlignCameraHorizontally = options.AlignCamera;
                previewsOptions.AlignCameraHorizontallyOffset =
                        options.AlignCameraOffset.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray()[0]; // TODO
                previewsOptions.SsaaMultiplier = options.SsaaMultiplier;
                previewsOptions.UseFxaa = options.UseFxaa;
                previewsOptions.UseMsaa = options.UseMsaa;
                previewsOptions.SoftwareDownsize = options.SoftwareDownsize;
                previewsOptions.MsaaSampleCount = options.MsaaSampleCount;
                previewsOptions.PreviewWidth = options.PreviewWidth;
                previewsOptions.PreviewHeight = options.PreviewHeight;
                previewsOptions.BloomRadiusMultiplier = options.BloomRadiusMultiplier;
                previewsOptions.FlatMirror = options.FlatMirror;
                previewsOptions.WireframeMode = options.WireframeMode;
                previewsOptions.MeshDebugMode = options.MeshDebugMode;
                previewsOptions.SuspensionDebugMode = options.SuspensionDebugMode;
                previewsOptions.HeadlightsEnabled = options.HeadlightsEnabled;
                previewsOptions.BrakeLightsEnabled = options.BrakeLightsEnabled;
                previewsOptions.LeftDoorOpen = options.LeftDoorOpen;
                previewsOptions.RightDoorOpen = options.RightDoorOpen;
                previewsOptions.SteerDeg = options.SteerAngle;
                previewsOptions.CameraPosition = options.CameraPosition.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray();
                previewsOptions.CameraLookAt = options.LookAt.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray();
                previewsOptions.CameraFov = options.Fov;
                previewsOptions.BackgroundColor = ParseColor(options.BackgroundColor);
                previewsOptions.LightColor = ParseColor(options.LightColor);
                previewsOptions.AmbientUp = ParseColor(options.AmbientUp);
                previewsOptions.AmbientDown = ParseColor(options.AmbientDown);
                previewsOptions.AmbientBrightness = options.AmbientBrightness;
                previewsOptions.LightBrightness = options.LightBrightness;
                previewsOptions.DelayedConvertation = !options.SingleThread;
                previewsOptions.UseSslr = options.UseSslr;
                previewsOptions.UseAo = options.UseSsao;
                previewsOptions.UsePcss = options.UsePcss;
                previewsOptions.EnableShadows = options.EnableShadows;
                previewsOptions.ShadowMapSize = options.ShadowMapSize;
                previewsOptions.MaterialsReflectiveness = options.ReflectionMultiplier;
                previewsOptions.ReflectionCubemapAtCamera = options.ReflectionCubemapAtCamera;
                previewsOptions.ReflectionsWithShadows = !options.NoShadowsWithReflections;
                previewsOptions.FlatMirrorBlurred = options.FlatMirrorBlurred;
                previewsOptions.FlatMirrorReflectiveness = options.FlatMirrorReflectiveness;
                previewsOptions.LightDirection = options.LightDirection.Split(',').Select(x => FlexibleParser.TryParseDouble(x) ?? 0d).ToArray();
            }

            Console.WriteLine($"showroom={options.Showroom}");

            using (var thing = new DarkPreviewsUpdater(acRoot, previewsOptions)) {
                foreach (var carId in Directory.GetDirectories(AcPaths.GetCarsDirectory(acRoot))
                                            .Select(Path.GetFileName).Where(x => filter.Test(x))) {
                    Console.WriteLine($"  {carId}...");
                    j++;

                    foreach (var skinId in Directory.GetDirectories(AcPaths.GetCarSkinsDirectory(acRoot, carId))
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
