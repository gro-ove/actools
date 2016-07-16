using System;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class UriToCachedImageConverter : IValueConverter {
        public const double OneTrueDpi = 96d;

        public static BitmapSource ConvertBitmapToOneTrueDpi(BitmapImage bitmapImage) {
            var width = bitmapImage.PixelWidth;
            var height = bitmapImage.PixelHeight;

            var stride = width * bitmapImage.Format.BitsPerPixel;
            var pixelData = new byte[stride * height];
            bitmapImage.CopyPixels(pixelData, stride, 0);

            return BitmapSource.Create(width, height, OneTrueDpi, OneTrueDpi, bitmapImage.Format, null, pixelData, stride);
        }

        public static BitmapSource Convert(object value, bool considerOneTrueDpi = false, int decodeWidth = -1, int decodeHeight = -1) {
            if (value is BitmapSource) return value as BitmapImage;

            var source = value as Uri;
            if (source == null) {
                var path = value?.ToString();
                if (string.IsNullOrEmpty(path)) {
                    return null;
                }

                try {
                    source = new Uri(path);
                } catch (Exception) {
                    Logging.Warning("[UriToCachedImageConverter] Invalid URI format: " + path);
                    return null;
                }
            }

            try {
                var bi = new BitmapImage();
                bi.BeginInit();

                if (decodeWidth != -1) {
                    bi.DecodePixelWidth = (int)(decodeWidth * DpiAwareWindow.OptionScale);
                }

                if (decodeHeight != -1) {
                    bi.DecodePixelHeight = (int)(decodeHeight * DpiAwareWindow.OptionScale);
                }

                bi.CreateOptions = source.Scheme == "http" || source.Scheme == "https"
                        ? BitmapCreateOptions.None : BitmapCreateOptions.IgnoreImageCache;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = source;
                bi.EndInit();
                bi.Freeze();

                if (considerOneTrueDpi && (Math.Abs(bi.DpiX - OneTrueDpi) > 1 || Math.Abs(bi.DpiY - OneTrueDpi) > 1)) {
                    return ConvertBitmapToOneTrueDpi(bi);
                }

                return bi;
            } catch (Exception) {
                return null;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var p = parameter as string;
            if (p == null) return Convert(value);

            var i = p.IndexOf('×');
            return i != -1
                    ? Convert(value, false, i == 0 ? -1 : p.Substring(0, i).AsInt(), i == p.Length - 1 ? -1 : p.Substring(i + 1).AsInt())
                    : Convert(value, p == "oneTrueDpi");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
