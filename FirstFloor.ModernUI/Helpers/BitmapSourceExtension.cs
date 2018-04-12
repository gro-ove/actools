using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class BitmapSourceExtension {
        public static BitmapFrame Resize(this BitmapSource bitmap, int width, int height, BitmapScalingMode scalingMode = BitmapScalingMode.HighQuality) {
            var group = new DrawingGroup();
            RenderOptions.SetBitmapScalingMode(group, scalingMode);
            group.Children.Add(new ImageDrawing(bitmap, new Rect(0, 0, width, height)));
            var targetVisual = new DrawingVisual();
            var targetContext = targetVisual.RenderOpen();
            targetContext.DrawDrawing(group);
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);
            targetContext.Close();
            target.Render(targetVisual);
            var targetFrame = BitmapFrame.Create(target);
            return targetFrame;
        }

        [NotNull]
        private static BitmapEncoder GetEncoder([NotNull] ImageFormat format) {
            if (Equals(format, ImageFormat.Png)) return new PngBitmapEncoder();
            if (Equals(format, ImageFormat.Jpeg)) return new JpegBitmapEncoder();
            if (Equals(format, ImageFormat.Bmp)) return new BmpBitmapEncoder();
            if (Equals(format, ImageFormat.Tiff)) return new TiffBitmapEncoder();
            if (Equals(format, ImageFormat.Gif)) return new GifBitmapEncoder();
            throw new NotSupportedException();
        }

        [NotNull]
        public static ImageFormat GetImageFormat([CanBeNull] this string filename) {
            switch (Path.GetExtension(filename)?.ToLowerInvariant()) {
                case "png":
                    return ImageFormat.Png;
                case "bmp":
                    return ImageFormat.Bmp;
                case "gif":
                    return ImageFormat.Gif;
                case "jpg":
                case "jpeg":
                    return ImageFormat.Jpeg;
                case "tif":
                case "tiff":
                    return ImageFormat.Tiff;
                default:
                    return ImageFormat.Png;
            }
        }

        public static void SaveTo([NotNull] this BitmapSource bitmap, [NotNull] string filename, [CanBeNull] ImageFormat format = null) {
            var encoder = GetEncoder(format ?? filename.GetImageFormat());
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var s = File.Create(filename)) {
                encoder.Save(s);
            }
        }

        [NotNull]
        public static byte[] ToBytes([NotNull] this BitmapSource bitmap, [NotNull] ImageFormat format) {
            var encoder = GetEncoder(format);
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var s = new MemoryStream()) {
                encoder.Save(s);
                return s.ToArray();
            }
        }
    }
}