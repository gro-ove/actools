using System;
using System.IO;
using System.Threading;
using AcTools.Utils;
using ImageMagick;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    // Requires Magick.NET
    public partial class DarkKn5ObjectRenderer {
        public static double OptionMaxMultipler = 1d;
        public static float OptionGBufferExtra = 2f;
        public static bool OptionHwCrop = true;

        private delegate void SplitCallback(Action<Stream> stream, int x, int y, int width, int height);

        private void SplitShot(double multiplier, double downscale, SplitCallback callback) {
            var original = new { Width, Height, ResolutionMultiplier, TimeFactor, CutScreenshot };
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
                    var x = i % cuts;
                    var y = i / cuts;

                    var xR = 2f * x / (cuts - 1) - 1f;
                    var yR = 1f - 2f * y / (cuts - 1);

                    Camera.CutProj = Matrix.Transformation2D(new Vector2(xR, yR), 0f, new Vector2(cuts), Vector2.Zero, 0f, Vector2.Zero) * baseCut;
                    Camera.SetLens(Camera.Aspect);

                    callback(s => {
                        CutScreenshot = OptionHwCrop && expand ? OptionGBufferExtra : 1d;
                        Shot(halfMultiplier, downscale, s, true);
                    }, x, y, (original.Width * downscale).RoundToInt(), (original.Height * downscale).RoundToInt());

                }
            } finally {
                Camera.CutProj = null;
                Camera.SetLens(Camera.Aspect);

                Width = original.Width;
                Height = original.Height;
                ResolutionMultiplier = original.ResolutionMultiplier;
                TimeFactor = original.TimeFactor;
                CutScreenshot = original.CutScreenshot;
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

            var total = cuts * cuts;
            var index = 0;

            SplitShot(multiplier, downscale, (c, x, y, width, height) => {
                progress?.Report(0.1 + 0.8 * index++ / total);
                if (cancellation.IsCancellationRequested) return;

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
            });

            return result;
        }
        
        public void SplitShot(double multiplier, double downscale, string destination, bool softwareDownscale, IProgress<double> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var original = new { Width, Height };
            var expand = UseSslr || UseAo;
            var cuts = Math.Ceiling(multiplier / OptionMaxMultipler).FloorToInt();
            var magickMode = softwareDownscale || !OptionHwCrop;
            var extension = magickMode ? "jpg" : "png";

            Directory.CreateDirectory(destination);

            var total = cuts * cuts;
            var index = 0;

            SplitShot(multiplier, downscale, (c, x, y, width, height) => {
                progress?.Report(0.1 + 0.8 * index++ / total);
                if (cancellation.IsCancellationRequested) return;

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
            });

            File.WriteAllText(Path.Combine(destination, "join.bat"), $@"@echo off
rem Use montage.exe from ImageMagick for Windows to run this script 
rem and combine images: https://www.imagemagick.org/script/binary-releases.php
montage.exe *-*.{extension} -tile {cuts}x{cuts} -geometry +0+0 out.jpg
echo @del *-*.{extension} delete-pieces.bat join.bat > delete-pieces.bat");
        }
    }
}