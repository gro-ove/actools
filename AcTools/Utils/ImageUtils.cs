using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using ImageMagick;

namespace AcTools.Utils {
    public static partial class ImageUtils {
        private static string _imageMagickAssemblyFilename;

        public static bool IsMagickAsseblyLoaded => _imageMagickAssemblyFilename != null;

        public static string TestImageMagick() {
            return "loaded, version: " + MagickNET.Version;
        }

        public static void LoadImageMagickAssembly(string dllFilename) {
            if (IsMagickAsseblyLoaded) return;

            try {
                _imageMagickAssemblyFilename = dllFilename;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            } catch (Exception) {
                _imageMagickAssemblyFilename = null;
                throw;
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            return args.Name.Contains("Magick.NET") ? Assembly.LoadFrom(_imageMagickAssemblyFilename) : null;
        }

        public static void UnloadImageMagickAssembly() {
            if (!IsMagickAsseblyLoaded) return;
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        private const int ApplyPreviewsWidth = 1024;
        private const int ApplyPreviewsHeight = 575;

        private static void ApplyPreviewImageMagick(string source, string destination, bool resize) {
            using (var image = new MagickImage(source)) {
                if (resize) {
                    var k = Math.Max((double)ApplyPreviewsHeight / image.Height, (double)ApplyPreviewsWidth / image.Width);
                    image.Resize((int)(k * image.Width), (int)(k * image.Height));
                    image.Crop(ApplyPreviewsWidth, ApplyPreviewsHeight, Gravity.Center);
                }

                image.Quality = 95;
                image.Density = new MagickGeometry(96, 96);
                image.Write(destination);
            }
        }

        public static void ApplyPreview(string source, string destination, bool resize) {
            if (File.Exists(destination)) {
                File.Delete(destination);
            }

            if (IsMagickAsseblyLoaded) {
                ApplyPreviewImageMagick(source, destination, resize);
            } else {
                var encoder = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                var parameters = new EncoderParameters(1) {Param = {[0] = new EncoderParameter(Encoder.Quality, 100L)}};

                if (resize) {
                    using (var bitmap = new Bitmap(ApplyPreviewsWidth, ApplyPreviewsHeight))
                    using (var graphics = Graphics.FromImage(bitmap)) {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        using (var image = Image.FromFile(source)) {
                            var k = Math.Max((double) ApplyPreviewsHeight/image.Height,
                                             (double) ApplyPreviewsWidth/image.Width);
                            graphics.DrawImage(image, (int) (0.5*(ApplyPreviewsWidth - k*image.Width)),
                                               (int) (0.5*(ApplyPreviewsHeight - k*image.Height)),
                                               (int) (k*image.Width), (int) (k*image.Height));
                        }

                        bitmap.Save(destination, encoder, parameters);
                    }
                } else {
                    using (var image = Image.FromFile(source)) {
                        image.Save(destination, encoder, parameters);
                    }
                }
            }

            try {
                File.Delete(source);
            } catch (Exception) {
                // ignored
            }
        }

        public static void ApplyPreviews(string acRoot, string carName, string source, bool resize) {
            foreach (var file in Directory.GetFiles(source, "*.bmp")) {
                var skinDirectory = FileUtils.GetCarSkinDirectory(acRoot, carName,
                                                               Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(skinDirectory)) continue;
                ApplyPreview(file, Path.Combine(skinDirectory, "preview.jpg"), resize);
            }
        }

        public static void ResizeFile(string source, string destination, int width, int height) {
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap)) {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var image = Image.FromFile(source)) {
                    var k = Math.Min((double)image.Width / width, (double)image.Height / height);
                    var nw = (float)(image.Width / k);
                    var nh = (float)(image.Height / k);
                    graphics.DrawImage(image, (nw - width) / 2f, (nh - height) / 2f, nw, nh);
                }

                bitmap.Save(destination, ImageFormat.Png);
            }
        }
    }
}
