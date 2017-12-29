using System;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class DrawingToColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as Color?)?.ToColor();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as System.Windows.Media.Color?)?.ToColor();
        }
    }
}