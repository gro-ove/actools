using System;
using System.IO;
using System.Threading;
using AcTools.Render.Base;
using AcTools.Render.Special;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    // Requires Magick.NET
    public partial class DarkKn5ObjectRenderer {
        public static string OptionTemporaryDirectory = Path.Combine(Path.GetTempPath(), "CMShowroom");
        public static double OptionMaxWidth = 3840;
        public static float OptionGBufferExtra = 1.2f;

        private delegate void SplitCallback(Action<Stream> stream, int x, int y, int width, int height);

        private void SplitShot(int width, int height, double downscale, RendererShotFormat format, SplitCallback callback, out int cuts,
                [CanBeNull] IProgress<Tuple<string, double?>> progress, CancellationToken cancellation) {
            ShotInProcessValue++;

            var original = new { Width, Height, ResolutionMultiplier, TimeFactor };
            ResolutionMultiplier = 1d;
            AutoAdjustTarget = false;
            AutoRotate = false;
            TimeFactor = 0f;

            cuts = Math.Ceiling(width / OptionMaxWidth).FloorToInt();
            if (cuts < 1) {
                cuts = 1;
            }

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
                        progress?.Report(new Tuple<string, double?>($"x={x}, y={y}, piece by piece", (double)i / (cuts * cuts) * 0.5));
                        if (cancellation.IsCancellationRequested) return;

                        SetCutProjection(cuts, x, y, baseCut);
                        var filename = Path.Combine(temporary, $"tmp-{y:D2}-{x:D2}{format.GetExtension()}");
                        using (var stream = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                            Shot(extraWidth, extraHeight, downscale, 1d, stream, format);
                        }
                    }

                    var shotWidth = (width * downscale).RoundToInt();
                    var shotHeight = (height * downscale).RoundToInt();
                    AcToolsLogging.Write($"Rendered: downscale={downscale}, {shotWidth}×{shotHeight}, {LastShotWidth}×{LastShotHeight}");

                    using (var blender = new PiecesBlender(shotWidth, shotHeight, OptionGBufferExtra)) {
                        blender.Initialize();

                        for (var i = 0; i < cuts * cuts; i++) {
                            var x = i % cuts;
                            var y = i / cuts;
                            progress?.Report(new Tuple<string, double?>($"X={x}, Y={y}, smoothing", (double)i / (cuts * cuts) * 0.5 + 0.5));
                            if (cancellation.IsCancellationRequested) return;

                            callback(s => blender.Process(new Pieces(temporary, $"tmp-{{0:D2}}-{{1:D2}}{format.GetExtension()}", y, x), s),
                                    x, y, shotWidth, shotHeight);
                        }
                    }

                    AcToolsLogging.Write("Blended");
                } else {
                    for (var i = 0; i < cuts * cuts; i++) {
                        var x = i % cuts;
                        var y = i / cuts;
                        progress?.Report(new Tuple<string, double?>($"X={x}, Y={y}", (double)i / (cuts * cuts)));
                        if (cancellation.IsCancellationRequested) return;

                        SetCutProjection(cuts, x, y, baseCut);
                        callback(s => Shot(extraWidth, extraHeight, downscale, expand ? OptionGBufferExtra : 1d, s, format),
                                x, y, (original.Width * downscale).RoundToInt(), (original.Height * downscale).RoundToInt());
                    }
                }
            } finally {
                Camera.CutProj = null;
                Camera.SetLens(Camera.Aspect);

                Width = original.Width;
                Height = original.Height;
                ResolutionMultiplier = original.ResolutionMultiplier;
                TimeFactor = original.TimeFactor;

                ShotInProcessValue--;
            }
        }

        private void SetCutProjection(int cuts, int x, int y, Matrix baseCut) {
            if (cuts > 1) {
                var xR = 2f * x / (cuts - 1) - 1f;
                var yR = 1f - 2f * y / (cuts - 1);
                Camera.CutProj = Matrix.Transformation2D(new Vector2(xR, yR), 0f, new Vector2(cuts), Vector2.Zero, 0f, Vector2.Zero) * baseCut;
            } else {
                Camera.CutProj = baseCut;
            }
            Camera.SetLens(Camera.Aspect);
        }

        public class SplitShotInformation {
            public RendererShotFormat Format;
            public int Cuts;

            public string GetMagickCommand(long memoryLimit) {
                var extension = Format.GetExtension();
                var limit = memoryLimit.ToInvariantString();
                var cuts = Cuts.ToInvariantString();
                return $"montage piece-*-*{extension} -limit memory {limit} -limit map {limit} -tile {cuts}x{cuts} -background none -geometry +0+0 out{extension}";
            }
        }

        public SplitShotInformation SplitShot(int width, int height, double downscale, string destination, RendererShotFormat format,
                IProgress<Tuple<string, double?>> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            Directory.CreateDirectory(destination);
            SplitShot(width, height, downscale, format, (c, x, y, w, h) => {
                var filename = Path.Combine(destination, $"piece-{y:D2}-{x:D2}{format.GetExtension()}");
                using (var stream = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                    c(stream);
                }
            }, out var cuts, progress, cancellation);

            return new SplitShotInformation {
                Cuts = cuts,
                Format = format
            };
        }
    }
}