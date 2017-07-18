using System;
using System.IO;
using System.Threading;
using AcTools.Render.Special;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    // Requires Magick.NET
    public partial class DarkKn5ObjectRenderer {
        public static string OptionTemporaryDirectory = Path.Combine(Path.GetTempPath(), "CMShowroom");
        public static double OptionMaxWidth = 3840;
        public static float OptionGBufferExtra = 1.2f;

        private delegate void SplitCallback(Action<Stream> stream, int x, int y, int width, int height);

        private void SplitShot(int width, int height, double downscale, SplitCallback callback, out int cuts, [CanBeNull] IProgress<Tuple<string, double?>> progress,
                CancellationToken cancellation) {
            var original = new { Width, Height, ResolutionMultiplier, TimeFactor };
            ResolutionMultiplier = 1d;
            AutoAdjustTarget = false;
            AutoRotate = false;
            TimeFactor = 0f;

            cuts = Math.Ceiling(width / OptionMaxWidth).FloorToInt();
            width /= cuts;
            height /= cuts;

            var expand = UseSslr || UseAo;
            int extraWidth, extraHeight;
            if (expand) {
                extraWidth = (width * OptionGBufferExtra).RoundToInt();
                extraHeight = (height * OptionGBufferExtra).RoundToInt();
            } else {
                extraWidth = width;
                extraHeight = height;
            }

            Width = extraWidth;
            Height = extraHeight;

            var baseCut = expand ?
                    Matrix.Transformation2D(Vector2.Zero, 0f, new Vector2(1f / OptionGBufferExtra), Vector2.Zero, 0f, Vector2.Zero) :
                    Matrix.Identity;

            try {
                var temporary = OptionTemporaryDirectory;
                if (expand && temporary != null) {
                    FileUtils.EnsureDirectoryExists(temporary);

                    for (var i = 0; i < cuts * cuts; i++) {
                        var x = i % cuts;
                        var y = i / cuts;
                        progress?.Report(new Tuple<string, double?>($"X={x}, Y={y}, piece by piece", (double)i / (cuts * cuts) * 0.5));
                        if (cancellation.IsCancellationRequested) return;

                        var xR = 2f * x / (cuts - 1) - 1f;
                        var yR = 1f - 2f * y / (cuts - 1);
                        Camera.CutProj = Matrix.Transformation2D(new Vector2(xR, yR), 0f, new Vector2(cuts), Vector2.Zero, 0f, Vector2.Zero) * baseCut;
                        Camera.SetLens(Camera.Aspect);

                        var filename = Path.Combine(temporary, $"tmp-{y:D2}-{x:D2}.png");
                        using (var stream = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                            Shot(extraWidth, extraHeight, downscale, 1d, stream, true);
                        }
                    }

                    var shotWidth = (width * downscale).RoundToInt();
                    var shotHeight = (height * downscale).RoundToInt();
                    AcToolsLogging.Write($"Rendered: downscale={downscale}, {shotWidth}×{shotHeight}, {LastShotWidth}×{LastShotHeight}");

                    using (var blender = new PiecesBlender(shotWidth, shotHeight, OptionGBufferExtra)){
                        blender.Initialize();

                        for (var i = 0; i < cuts * cuts; i++) {
                            var x = i % cuts;
                            var y = i / cuts;
                            progress?.Report(new Tuple<string, double?>($"X={x}, Y={y}, smoothing", (double)i / (cuts * cuts) * 0.5 + 0.5));
                            if (cancellation.IsCancellationRequested) return;

                            callback(s => {
                                blender.Process(new Pieces(temporary, "tmp-{0:D2}-{1:D2}.png", y, x), s);
                            }, x, y, shotWidth, shotHeight);
                        }
                    }

                    AcToolsLogging.Write("Blended");
                } else {
                    for (var i = 0; i < cuts * cuts; i++) {
                        var x = i % cuts;
                        var y = i / cuts;
                        progress?.Report(new Tuple<string, double?>($"X={x}, Y={y}", (double)i / (cuts * cuts)));
                        if (cancellation.IsCancellationRequested) return;

                        var xR = 2f * x / (cuts - 1) - 1f;
                        var yR = 1f - 2f * y / (cuts - 1);
                        Camera.CutProj = Matrix.Transformation2D(new Vector2(xR, yR), 0f, new Vector2(cuts), Vector2.Zero, 0f, Vector2.Zero) * baseCut;
                        Camera.SetLens(Camera.Aspect);

                        callback(s => {
                            Shot(extraWidth, extraHeight, downscale, expand ? OptionGBufferExtra : 1d, s, true);
                        }, x, y, (original.Width * downscale).RoundToInt(), (original.Height * downscale).RoundToInt());

                    }
                }
            } finally {
                Camera.CutProj = null;
                Camera.SetLens(Camera.Aspect);

                Width = original.Width;
                Height = original.Height;
                ResolutionMultiplier = original.ResolutionMultiplier;
                TimeFactor = original.TimeFactor;
            }
        }

        /*public MagickImage SplitShot(int width, int height, double downscale, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var original = new { Width, Height };
            var cuts = Math.Ceiling(multiplier / OptionMaxMultipler).FloorToInt();
            var result = new MagickImage(MagickColors.Black,
                    (original.Width * cuts * downscale).RoundToInt(),
                    (original.Height * cuts * downscale).RoundToInt());

            SplitShot(multiplier, downscale, (c, x, y, width, height) => {
                using (var stream = new MemoryStream()) {
                    c(stream);
                    if (cancellation.IsCancellationRequested) return;

                    stream.Position = 0;
                    using (var piece = new MagickImage(stream)) {
                        result.Composite(piece, x * width, y * height);
                    }
                }
            }, progress, cancellation);
            return result;
        }*/

        public class SplitShotInformation {
            public string Extension;
            public int Cuts;
        }

        public SplitShotInformation SplitShot(int width, int height, double downscale, string destination, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            const string extension = "png";

            Directory.CreateDirectory(destination);
            SplitShot(width, height, downscale, (c, x, y, w, h) => {
                var filename = Path.Combine(destination, $"piece-{y:D2}-{x:D2}.{extension}");
                using (var stream = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                    c(stream);
                }
            }, out var cuts, progress, cancellation);

            return new SplitShotInformation {
                Cuts = cuts,
                Extension = extension
            };
        }
    }
}