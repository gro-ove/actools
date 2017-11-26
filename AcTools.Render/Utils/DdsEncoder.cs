using System;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using AcTools.Utils;
using DdsCompress;
using ImageMagick;

namespace AcTools.Render.Utils {
    public static class DdsEncoder {
        private static Compressor GetCompressor(PreferredDdsFormat format, int width, int height) {
            var result = new Compressor {
                MipmapGeneration = true,
                MipmapMaxLevel = -1,
                RoundMode = CompressRoundMode.None,
                Quality = CompressQuality.Normal,
                ResizeFilter = CompressResizeFilter.Mitchell
            };

            switch (format) {
                case PreferredDdsFormat.Auto:
                    if (width <= 512 && height <= 512) {
                        result.Format = CompressFormat.RGB;
                    } else {
                        goto case PreferredDdsFormat.DXT1;
                    }
                    break;
                case PreferredDdsFormat.AutoTransparency:
                    if (width <= 512 && height <= 512) {
                        result.Format = CompressFormat.RGBA;
                    } else {
                        goto case PreferredDdsFormat.DXT5;
                    }
                    break;
                case PreferredDdsFormat.DXT1:
                    result.Format = CompressFormat.DXT1;
                    result.RoundMode = CompressRoundMode.ToNearestMultipleOfFour;
                    break;
                case PreferredDdsFormat.DXT5:
                    result.Format = CompressFormat.DXT5;
                    result.RoundMode = CompressRoundMode.ToNearestMultipleOfFour;
                    break;
                /*case PreferredDdsFormat.GrayscaleDictionary:
                    break;*/
                case PreferredDdsFormat.NoCompression:
                    result.Format = CompressFormat.RGB;
                    break;
                case PreferredDdsFormat.NoCompressionTransparency:
                    result.Format = CompressFormat.RGBA;
                    break;
                case PreferredDdsFormat.Luminance:
                    result.Format = CompressFormat.Luminance;
                    break;
                case PreferredDdsFormat.LuminanceTransparency:
                    result.Format = CompressFormat.LuminanceAlpha;
                    break;
                case PreferredDdsFormat.RGB565:
                    result.Format = CompressFormat.RGB565;
                    break;
                case PreferredDdsFormat.RGBA4444:
                    result.Format = CompressFormat.A4R4G4B4;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            return result;
        }

        // TODO: Optimize!
        private static byte[] CopySafe(Bitmap bitmap){
            var w = bitmap.Width;
            var h = bitmap.Height;
            var r = new byte[w * h * 4];
            for (int i = 0, y = 0; y < h; y++) {
                for (var x = 0; x < w; x++) {
                    var c = bitmap.GetPixel(x, y);
                    r[i++] = c.B; r[i++] = c.G; r[i++] = c.R; r[i++] = c.A;
                }
            }
            return r;
        }

        private class CompressProgress : ICompressProgress {
            private readonly IProgress<double> _action;

            public CompressProgress(IProgress<double> action){
                _action = action;
            }

            public void Report(double value){
                _action?.Report(value);
            }
        }

        private class CompressLog : ICompressLog {
            private readonly Action<string> _action;

            public CompressLog(Action<string> action){
                _action = action;
            }

            public void Report(string value){
                _action?.Invoke(value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SaveAsDds_Compressor(Stream stream, byte[] imageData, PreferredDdsFormat format, IProgress<double> progress) {
            using (var input = new MemoryStream(imageData))
            using (var image = (Bitmap)Image.FromStream(input)) {
                GetCompressor(format, image.Width, image.Height).Process(image.Width, image.Height, CopySafe(image), stream,
                        new CompressProgress(progress),
                        new CompressLog(s => AcToolsLogging.Write(s)));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SaveAsDds_Magick(Stream stream, byte[] imageData, PreferredDdsFormat format) {
            using (var input = new MemoryStream(imageData))
            using (var image = new MagickImage(input)) {
                if (format == PreferredDdsFormat.DXT1) {
                    image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt1");
                    image.Settings.SetDefine(MagickFormat.Dds, "mipmaps", "false");
                    image.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", "true");
                } else {
                    image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt5");
                    image.Settings.SetDefine(MagickFormat.Dds, "mipmaps", "false");
                    image.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", "true");
                }

                image.Write(stream);
            }
        }

        public static void SaveAsDds(Stream stream, byte[] imageData, PreferredDdsFormat format, IProgress<double> progress) {
            try {
                SaveAsDds_Compressor(stream, imageData, format, progress);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                try {
                    SaveAsDds_Magick(stream, imageData, format);
                    AcToolsLogging.NonFatalErrorNotify("Can’t encode DDS texture using NVIDIA Texture Tools", null, e);
                } catch (Exception ex) {
                    AcToolsLogging.NonFatalErrorNotify("Can’t encode DDS texture using NVIDIA Texture Tools or Magick.NET", null, ex);
                }
            }
        }

        public static void SaveAsDds(string filename, byte[] imageData, PreferredDdsFormat format, IProgress<double> progress) {
            using (var temporary = FileUtils.RecycleOriginal(filename)) {
                try {
                    using (var stream = File.Create(temporary.Filename)) {
                        SaveAsDds(stream, imageData, format, progress);
                    }
                } catch {
                    FileUtils.TryToDelete(temporary.Filename);
                    throw;
                }
            }
        }
    }
}