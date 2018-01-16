using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(Brush), typeof(Brush))]
    public class BrushOpacityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is SolidColorBrush v ? new SolidColorBrush(v.Color) {
                Opacity = parameter.As(1d)
            } : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}