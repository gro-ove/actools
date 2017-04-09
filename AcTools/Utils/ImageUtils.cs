using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using ImageMagick;
using JetBrains.Annotations;
using Encoder = System.Drawing.Imaging.Encoder;

namespace AcTools.Utils {
    public class AcPreviewImageInformation {
        [CanBeNull]
        public string Name { get; set; }

        [CanBeNull]
        public string Style { get; set; }

        public string ExifStyle => Style == null ? null : "Style: " + Style;
    }

    public static partial class ImageUtils {
        private static bool? _isMagickSupported;

        public static bool IsMagickSupported => _isMagickSupported ?? (_isMagickSupported = CheckIfMagickSupported()).Value;

        private static bool CheckIfMagickSupported() {
            try {
                return TestImageMagick() != null;
            } catch (Exception) {
                return false;
            }
        }

        private static string _imageMagickAssemblyFilename;

        public static bool IsMagickAsseblyLoaded => _imageMagickAssemblyFilename != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string TestImageMagick() {
            return "loaded, version: " + MagickNET.Version;
        }

        public static void LoadImageMagickAssembly(string dllFilename) {
            if (IsMagickAsseblyLoaded) return;
            _isMagickSupported = null;

            try {
                _imageMagickAssemblyFilename = dllFilename;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            } catch (Exception) {
                _imageMagickAssemblyFilename = null;
                throw;
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            return args.Name.Contains("Magick.NET") && _imageMagickAssemblyFilename != null ? Assembly.LoadFrom(_imageMagickAssemblyFilename) : null;
        }

        public static void UnloadImageMagickAssembly() {
            if (!IsMagickAsseblyLoaded) return;

            _isMagickSupported = null;
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        // Temporary solution?
        public static Func<Func<object>, object> SafeMagickWrapper { get; set; }

        private const MagickFormat CommonFormat = MagickFormat.Bmp;

        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadAsConventionalBuffer(byte[] data, bool wrapping = true) {
            if (wrapping && SafeMagickWrapper != null) {
                return SafeMagickWrapper(() => LoadAsConventionalBuffer(data, false)) as byte[];
            }
            
            using (var image = new MagickImage(data)) {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                return image.ToByteArray(CommonFormat);
            }
        }

        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadAsConventionalBuffer(byte[] data, bool noAlpha, out string formatDescription, bool wrapping = true) {
            if (wrapping && SafeMagickWrapper != null) {
                string description = null;
                var result = SafeMagickWrapper(() => LoadAsConventionalBuffer(data, noAlpha, out description, false)) as byte[];
                formatDescription = description;
                return result;
            }
            
            using (var image = new MagickImage(data)) {
                formatDescription = image.CompressionMethod.ToString();

                if (noAlpha) {
                    image.HasAlpha = false;
                }

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                return image.ToByteArray(CommonFormat);
            }
        }

        public static void ApplyPreview(string source, string destination, bool resize, [CanBeNull] AcPreviewImageInformation information) {
            if (resize) {
                ApplyPreview(source, destination, CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight, information);
            } else {
                ApplyPreview(source, destination, 0, 0, information);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ApplyPreviewImageMagick(string source, string destination, double maxWidth, double maxHeight,
                [CanBeNull] AcPreviewImageInformation information) {
            using (var image = new MagickImage(source)) {
                if (maxWidth > 0d || maxHeight > 0d) {
                    var k = Math.Max(maxHeight / image.Height, maxWidth / image.Width);
                    image.Interpolate = PixelInterpolateMethod.Catrom;
                    image.FilterType = FilterType.Lanczos;
                    image.Sharpen();
                    image.Resize((int)(k * image.Width), (int)(k * image.Height));
                    image.Crop((int)maxWidth, (int)maxHeight, Gravity.Center);
                }

                image.Quality = 95;
                image.Density = new Density(96, 96);
                if (File.Exists(destination)) {
                    try {
                        File.Delete(destination);
                    } catch (UnauthorizedAccessException) {
                        Thread.Sleep(200);
                        File.Delete(destination);
                    }
                }

                var profile = new ExifProfile();
                profile.SetValue(ExifTag.Software, AcToolsInformation.Name);

                if (information?.ExifStyle != null) {
                    profile.SetValue(ExifTag.Artist, information.ExifStyle);
                }

                if (information?.Name != null) {
                    profile.SetValue(ExifTag.ImageDescription, information.Name);
                }

                image.AddProfile(profile);
                image.Write(destination);
            }
        }

        public static void ApplyPreview(string source, string destination, double maxWidth, double maxHeight, [CanBeNull] AcPreviewImageInformation information,
                bool keepOriginal = false) {
            if (File.Exists(destination)) {
                File.Delete(destination);
            }

            if (IsMagickSupported) {
                ApplyPreviewImageMagick(source, destination, maxWidth, maxHeight, information);
            } else {
                var encoder = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                var parameters = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(Encoder.Quality, 100L) } };

                if (maxWidth > 0d || maxHeight > 0d) {
                    using (var bitmap = new Bitmap((int)maxWidth, (int)maxHeight))
                    using (var graphics = Graphics.FromImage(bitmap)) {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        using (var image = Image.FromFile(source)) {
                            var k = Math.Max(maxHeight / image.Height, maxWidth / image.Width);
                            graphics.DrawImage(image, (int)(0.5 * ((int)maxWidth - k * image.Width)),
                                    (int)(0.5 * ((int)maxHeight - k * image.Height)),
                                    (int)(k * image.Width), (int)(k * image.Height));
                        }

                        new ExifWorks(bitmap) {
                            Software = AcToolsInformation.Name,
                            Description = information?.Name,
                            Artist = information?.ExifStyle
                        }.Dispose();

                        bitmap.Save(destination, encoder, parameters);
                    }
                } else {
                    using (var image = (Bitmap)Image.FromFile(source)) {
                        image.Save(destination, encoder, parameters);
                    }
                }
            }

            GCHelper.CleanUp();
            if (!keepOriginal) {
                try {
                    File.Delete(source);
                } catch (Exception) {
                    // ignored
                }
            }
        }

        public static void ApplyPreviews([NotNull] string acRoot, [NotNull] string carName, [NotNull] string source, bool resize,
                [CanBeNull] AcPreviewImageInformation information) {
            foreach (var file in Directory.GetFiles(source, "*.bmp")) {
                var skinDirectory = FileUtils.GetCarSkinDirectory(acRoot, carName,
                        Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(skinDirectory)) continue;
                ApplyPreview(file, Path.Combine(skinDirectory, "preview.jpg"), resize, information);
            }
        }

        public static async Task ApplyPreviewsAsync([NotNull] string acRoot, [NotNull] string carName, [NotNull] string source, bool resize,
                [CanBeNull] AcPreviewImageInformation information,
                IProgress<Tuple<string, double?>> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            var files = Directory.GetFiles(source, "*.bmp");
            for (var i = 0; i < files.Length; i++) {
                var file = files[i];
                var id = Path.GetFileNameWithoutExtension(file);
                var skinDirectory = FileUtils.GetCarSkinDirectory(acRoot, carName, id);
                if (!Directory.Exists(skinDirectory)) continue;

                progress?.Report(new Tuple<string, double?>(id, (double)i / files.Length));
                await Task.Run(() => {
                    ApplyPreview(file, Path.Combine(skinDirectory, "preview.jpg"), resize, information);
                }, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }

            try {
                Directory.Delete(source);
            } catch (Exception) {
                // ignored
            }
        }

        public static Image HighQualityResize(this Image img, Size size) {
            var percent = Math.Min(size.Width / (float)img.Width, size.Height / (float)img.Height);
            var width = (int)(img.Width * percent);
            var height = (int)(img.Height * percent);

            var b = new Bitmap(width, height);
            using (var g = Graphics.FromImage(b)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, width, height);
            }

            return b;
        }

        public class ImageInformation {
            [CanBeNull]
            public string Software, Copyright, Title, Author, Subject, Comment;

            [CanBeNull]
            public string[] Tags;
            public double? Rating;
        }

        public static void Downscale(this MagickImage image, double scale = 0.5) {
            image.Interpolate = PixelInterpolateMethod.Catrom;
            image.FilterType = FilterType.Lanczos;
            image.Sharpen();
            image.Resize(new Percentage(scale * 100d));
        }

        public static void SaveImage(MagickImage image, string destination, int quality = 95, ImageInformation exif = null,
                MagickFormat format = MagickFormat.Jpeg) {
            using (var stream = File.Open(destination, FileMode.Create, FileAccess.ReadWrite)) {
                SaveImage(image, stream, quality, exif, format);
            }
        }

        public static void SaveImage(MagickImage image, Stream destination, int quality = 95, ImageInformation exif = null, MagickFormat format = MagickFormat.Jpeg) {
            if (exif != null) {
                var profile = new ExifProfile();

                profile.SetValue(ExifTag.Software, exif.Software == null ? AcToolsInformation.Name : $"{exif.Software} ({AcToolsInformation.Name})");
                if (exif.Copyright != null) profile.SetValue(ExifTag.Copyright, exif.Copyright);

                if (exif.Title != null) profile.SetValue(ExifTag.XPTitle, Encoding.Unicode.GetBytes(exif.Title));
                if (exif.Author != null) profile.SetValue(ExifTag.XPAuthor, Encoding.Unicode.GetBytes(exif.Author));
                if (exif.Tags?.Length > 0) profile.SetValue(ExifTag.XPKeywords, Encoding.Unicode.GetBytes(exif.Tags.JoinToString(';')));
                if (exif.Subject != null) profile.SetValue(ExifTag.XPSubject, Encoding.Unicode.GetBytes(exif.Subject));
                if (exif.Comment != null) profile.SetValue(ExifTag.XPComment, Encoding.Unicode.GetBytes(exif.Comment));

                if (exif.Rating.HasValue) {
                    profile.SetValue(ExifTag.Rating, (ushort)(exif.Rating.Value.Saturate() * 5.0).RoundToInt());
                }

                var date = DateTime.Now.ToString("yyyy\\:MM\\:dd HH\\:mm\\:ss");
                profile.SetValue(ExifTag.DateTime, date);
                profile.SetValue(ExifTag.DateTimeOriginal, date);
                profile.SetValue(ExifTag.DateTimeDigitized, date);

                image.AddProfile(profile);
            }

            image.Quality = quality;
            image.Density = new Density(96, 96);
            image.Write(destination, format);
        }

        private static void SaveImage(Image image, Stream destination, int quality, ImageInformation exif) {
            if (exif != null) {
                var date = DateTime.Now;

                var profile = new ExifWorks(image) {
                    Software = exif.Software == null ? AcToolsInformation.Name : $"{exif.Software} ({AcToolsInformation.Name})",
                    DateTimeOriginal = date,
                    DateTimeDigitized = date,
                    DateTimeLastModified = date
                };

                if (exif.Copyright != null) profile.Copyright = exif.Copyright;
                if (exif.Title != null) profile.Title = exif.Title;
                if (exif.Author != null) profile.Artist = exif.Author;
                if (exif.Subject != null) profile.Subject = exif.Subject;
                if (exif.Comment != null) profile.UserComment = exif.Comment;
            }

            var encoder = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
            var parameters = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(Encoder.Quality, quality > 85 ? 100L : quality) } };
            image.Save(destination, encoder, parameters);
        }

        public static bool OptionConvertCombined = true;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ConvertImageMagick(Stream source, Stream destination, Size? resize, int quality, ImageInformation exif) {
            using (var image = new MagickImage(source)) {
                if (resize.HasValue) {
                    var k = Math.Max((double)resize.Value.Height / image.Height, (double)resize.Value.Width / image.Width);
                    image.Interpolate = PixelInterpolateMethod.Catrom;
                    image.FilterType = FilterType.Lanczos;
                    image.Sharpen();
                    image.Resize((int)(k * image.Width), (int)(k * image.Height));
                    image.Crop(resize.Value.Width, resize.Value.Height, Gravity.Center);
                }

                SaveImage(image, destination, quality, exif);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ConvertCombined(Stream source, Stream destination, Size? resize, int quality, ImageInformation exif) {
            if (resize == null) {
                ConvertImageMagick(source, destination, null, quality, exif);
                return;
            }

            // Because Magick.NET is terribly slow at resizing big images…
            using (var memory = new MemoryStream()) {
                using (var image = Image.FromStream(source))
                using (var resized = image.HighQualityResize(resize.Value)) {
                    resized.Save(memory, ImageFormat.Bmp);
                }

                using (var image = new MagickImage(memory)) {
                    SaveImage(image, destination, quality, exif);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ConvertGdi(Stream source, Stream destination, Size? resize, int quality, ImageInformation exif) {
            using (var image = Image.FromStream(source)) {
                if (resize.HasValue) {
                    using (var resized = image.HighQualityResize(resize.Value)) {
                        SaveImage(resized, destination, quality, exif);
                    }
                } else {
                    SaveImage(image, destination, quality, exif);
                }
            }
        }

        public static void Convert(Stream source, Stream destination, Size? resize, int quality = 95, ImageInformation exif = null) {
            if (!IsMagickSupported) {
                ConvertGdi(source, destination, resize, quality, exif);
            } else if (OptionConvertCombined) {
                ConvertCombined(source, destination, resize, quality, exif);
            } else {
                ConvertImageMagick(source, destination, resize, quality, exif);
            }
        }

        public static void Convert(string source, string destination, Size? resize, int quality = 95, ImageInformation exif = null) {
            if (File.Exists(destination)) {
                try {
                    File.Delete(destination);
                } catch (UnauthorizedAccessException) {
                    Thread.Sleep(200);
                    File.Delete(destination);
                }
            }

            using (var sourceStream = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destinationStream = File.Open(destination, FileMode.Create, FileAccess.ReadWrite)) {
                Convert(sourceStream, destinationStream, resize, quality, exif);
            }
        }

        public static void Convert(Stream source, Stream destination, int quality = 95, ImageInformation exif = null) {
            Convert(source, destination, null, quality, exif);
        }

        public static void Convert(string source, string destination, int quality = 95, ImageInformation exif = null) {
            Convert(source, destination, null, quality, exif);
        }
    }
}
