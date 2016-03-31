using System;
using System.Windows.Data;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class UriToCachedImageConverter
        : IValueConverter {
        public const double OneTrueDpi = 96d;

        public static BitmapSource ConvertBitmapToOneTrueDpi(BitmapImage bitmapImage) {
            var width = bitmapImage.PixelWidth;
            var height = bitmapImage.PixelHeight;

            var stride = width * bitmapImage.Format.BitsPerPixel;
            var pixelData = new byte[stride * height];
            bitmapImage.CopyPixels(pixelData, stride, 0);

            return BitmapSource.Create(width, height, OneTrueDpi, OneTrueDpi, bitmapImage.Format, null, pixelData, stride);
        }

        public static BitmapSource Convert(object value, bool considerOneTrueDpi = false) {
            var source = value as Uri;

            if (source == null) {
                var path = value?.ToString();
                if (string.IsNullOrEmpty(path)) {
                    return null;
                }

                source = new Uri(path);
            }

            try {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = source.Scheme == "http" || source.Scheme == "https"
                        ? BitmapCreateOptions.None : BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreImageCache;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = source;
                bi.EndInit();

                if (considerOneTrueDpi && (Math.Abs(bi.DpiX - OneTrueDpi) > 1 || Math.Abs(bi.DpiY - OneTrueDpi) > 1)) {
                    return ConvertBitmapToOneTrueDpi(bi);
                }

                return bi;
            } catch (Exception) {
                return null;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Convert(value, parameter as string == "oneTrueDpi");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException("Two way conversion is not supported.");
        }
    }
}
