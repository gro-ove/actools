using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(object), typeof(BitmapSource))]
    public class IconPathToImageConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var path = value.As<string>();
            if (path == null) return null;
            try {
                return ToImageSource(Extract(Environment.ExpandEnvironmentVariables(path), true));
            } catch (Exception e) {
                Logging.Error(e.Message);
                return null;
            }
        }

        [DllImport("Shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall)]
        private static extern int ExtractIconEx(string sFile, int iIndex, out IntPtr piLargeVersion, out IntPtr piSmallVersion, int amountIcons);

        private static Icon Extract(string path, bool largeIcon) {
            var i = path.LastIndexOf(',');
            ExtractIconEx(i == -1 ? path : path.Substring(0, i), i == -1 ? 1 : path.Substring(i + 1).As<int>(), out var large, out var small, 1);
            return Icon.FromHandle(largeIcon ? large : small);
        }

        private static ImageSource ToImageSource(Icon icon) {
            return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}