using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CommandLine;
using StringBasedFilter;

namespace PreviewUpdater {
    internal class Program {
        private static PackedHelper _helper;

        [STAThread]
        private static int Main(string[] a) {
            _helper = new PackedHelper("AcTools_PreviewUpdater", "PreviewUpdater.References", false);
            AppDomain.CurrentDomain.AssemblyResolve += _helper.Handler;
            return MainInner(a);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MainInner(string[] args) {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options) || options.Ids.Count == 0) return 1;

            var acRoot = Path.GetFullPath(options.AcRoot);
            var magick = _helper.GetFilename("Magick.NET-x86");
            if (magick != null && File.Exists(magick)) {
                try {
                    ImageUtils.LoadImageMagickAssembly(magick);
                } catch (Exception e) {
                    Console.Error.WriteLine("Can’t load ImageMagick assembly: " + e.Message);
                }
            }

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
                Console.WriteLine("ImageMagick: " + (ImageUtils.IsMagickSupported ? "Available" : "Not available"));

                Console.WriteLine("AC root: " + options.AcRoot);
                Console.WriteLine("Showroom: " + options.Showroom);
                Console.WriteLine("Filter: " + options.Filter);
                Console.WriteLine("Camera position: " + options.CameraPosition);
                Console.WriteLine("Look at: " + options.LookAt);
                Console.WriteLine("FOV: " + options.Fov);
                Console.WriteLine("Exposure: " + options.Exposure);
                Console.WriteLine("FXAA: " + options.Fxaa);
                Console.WriteLine("4K Resolution: " + options.SpecialResolution);
                Console.WriteLine("Maximize video: " + options.MaximizeVideo);

                Console.WriteLine("Starting shoting...");
            }

            foreach (var id in Directory.GetDirectories(FileUtils.GetCarsDirectory(options.AcRoot))
                                        .Select(Path.GetFileName).Where(x => filter.Test(x))) {
                string[] skinIds = options.WithoutPreviews
                        ? Directory.GetDirectories(FileUtils.GetCarSkinsDirectory(acRoot, id))
                                   .Where(x => !File.Exists(Path.Combine(x, "preview.jpg")))
                                   .Select(Path.GetFileName)
                                   .ToArray()
                        : null;

                if (options.Verbose) {
                    Console.WriteLine("Processing: " + id);

                    if (options.WithoutPreviews) {
                        Console.WriteLine("    Shot only: " + skinIds?.JoinToString(", "));
                    }
                }

                int i;
                for (i = 0; i < options.MaxAttempts; i++) {
                    try {
                        var shotted = Showroom.Shot(new Showroom.ShotProperties {
                            AcRoot = acRoot,
                            CarId = id,
                            ShowroomId = options.Showroom,
                            SkinIds = skinIds,
                            Filter = options.Filter,
                            Fxaa = options.Fxaa,
                            SpecialResolution = options.SpecialResolution,
                            MaximizeVideoSettings = options.MaximizeVideo,
                            Mode = Showroom.ShotMode.Fixed,
                            UseBmp = true,
                            DisableWatermark = true,
                            DisableSweetFx = true,
                            ClassicCameraDx = 0.0,
                            ClassicCameraDy = 0.0,
                            ClassicCameraDistance = 5.5,
                            FixedCameraPosition = options.CameraPosition,
                            FixedCameraLookAt = options.LookAt,
                            FixedCameraFov = options.Fov,
                            FixedCameraExposure = options.Exposure,
                        });

                        if (shotted != null) {
                            if (options.Verbose) {
                                Console.WriteLine("    Applying previews from: " + shotted);
                            }

                            ImageUtils.ApplyPreviews(acRoot, id, shotted, true, null);
                        } else {
                            if (options.Verbose) {
                                Console.WriteLine("    Nothing shotted");
                            }
                        }
                        
                        break;
                    } catch (ShotingCancelledException e) {
                        if (!e.UserCancelled) {
                            Console.Error.WriteLine(e.Message);
                        } else if (options.Verbose) {
                            Console.Error.WriteLine("Cancelled");
                        }

                        return 3;
                    } catch (Exception e) {
                        Console.Error.WriteLine(e);
                    }
                }

                if (i == options.MaxAttempts && !options.IgnoreErrors) {
                    Console.Error.WriteLine("Attempts number exceeded, terminating");
                    return 2;
                }
            }

            return 0;
        }
    }
}
