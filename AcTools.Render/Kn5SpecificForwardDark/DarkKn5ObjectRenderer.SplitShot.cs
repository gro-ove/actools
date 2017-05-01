using System;
using System.IO;
using System.Threading;
using AcTools.Utils;
using ImageMagick;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    // Requires Magick.NET
    public partial class DarkKn5ObjectRenderer {
        public static double OptionMaxMultipler = 1d;
        public static float OptionGBufferExtra = 2f;
        public static bool OptionHwCrop = true;

        private delegate void SplitCallback(Action<Stream> stream, int x, int y, int width, int height);

        private void SplitShot(double multiplier, double downscale, SplitCallback callback, [CanBeNull] IProgress<double> progress,
                CancellationToken cancellation) {
            var original = new { Width, Height, ResolutionMultiplier, TimeFactor };
            ResolutionMultiplier = 1d;
            AutoAdjustTarget = false;
            AutoRotate = false;
            TimeFactor = 0f;

            var expand = UseSslr || UseAo;
            if (expand) {
                Width = (Width * OptionGBufferExtra).RoundToInt();
                Height = (Height * OptionGBufferExtra).RoundToInt();
            }

            var baseCut = expand ?
                    Matrix.Transformation2D(Vector2.Zero, 0f, new Vector2(1f / OptionGBufferExtra), Vector2.Zero, 0f, Vector2.Zero) :
                    Matrix.Identity;

            try {
                var cuts = Math.Ceiling(multiplier / OptionMaxMultipler).FloorToInt();
                var halfMultiplier = multiplier / cuts;

                for (var i = 0; i < cuts * cuts; i++) {
                    progress?.Report(0.05 + 0.9 * i / (cuts * cuts));
                    if (cancellation.IsCancellationRequested) return;

                    var x = i % cuts;
                    var y = i / cuts;

                    var xR = 2f * x / (cuts - 1) - 1f;
                    var yR = 1f - 2f * y / (cuts - 1);

                    Camera.CutProj = Matrix.Transformation2D(new Vector2(xR, yR), 0f, new Vector2(cuts), Vector2.Zero, 0f, Vector2.Zero) * baseCut;
                    Camera.SetLens(Camera.Aspect);

                    callback(s => {
                        Shot(halfMultiplier, downscale, OptionHwCrop && expand ? OptionGBufferExtra : 1d, s, true);
                    }, x, y, (original.Width * downscale).RoundToInt(), (original.Height * downscale).RoundToInt());

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

        public MagickImage SplitShot(double multiplier, double downscale, IProgress<double> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var original = new { Width, Height };
            var expand = UseSslr || UseAo;
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
                        if (expand && !OptionHwCrop) {
                            piece.Crop(original.Width, original.Height, Gravity.Center);
                        }

                        result.Composite(piece, x * width, y * height);
                    }
                }
            }, progress, cancellation);
            return result;
        }

        public class SplitShotInformation {
            public string Extension;
            public int Cuts;
        }
        
        public SplitShotInformation SplitShot(double multiplier, double downscale, string destination, bool softwareDownscale, IProgress<double> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var original = new { Width, Height };
            var expand = UseSslr || UseAo;
            var cuts = Math.Ceiling(multiplier / OptionMaxMultipler).FloorToInt();
            var magickMode = softwareDownscale || !OptionHwCrop;
            var extension = magickMode ? "jpg" : "png";

            Directory.CreateDirectory(destination);
            SplitShot(multiplier, downscale, (c, x, y, width, height) => {
                var filename = Path.Combine(destination, $"{y}-{x}.{extension}");
                if (magickMode) {
                    using (var stream = new MemoryStream()) {
                        c(stream);
                        if (cancellation.IsCancellationRequested) return;

                        stream.Position = 0;
                        using (var piece = new MagickImage(stream)) {
                            if (expand && !OptionHwCrop) {
                                piece.Crop(original.Width, original.Height, Gravity.Center);
                            }

                            piece.Downscale();
                            ImageUtils.SaveImage(piece, filename, exif: new ImageUtils.ImageInformation());
                        }
                    }
                } else {
                    using (var stream = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                        c(stream);
                    }
                }
            }, progress, cancellation);

            return new SplitShotInformation {
                Cuts = cuts,
                Extension = extension
            };
        }
    }
}