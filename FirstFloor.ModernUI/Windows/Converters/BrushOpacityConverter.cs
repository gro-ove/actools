using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(Brush), typeof(Brush))]
    public class BrushOpacityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var v = value as SolidColorBrush;
            return v == null ? null : new SolidColorBrush(v.Color) {
                Opacity = parameter.AsDouble(1d)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}