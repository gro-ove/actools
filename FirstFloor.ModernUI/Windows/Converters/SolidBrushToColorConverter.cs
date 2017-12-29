using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(SolidColorBrush), typeof(Color))]
    public class SolidBrushToColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as SolidColorBrush)?.Color;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}