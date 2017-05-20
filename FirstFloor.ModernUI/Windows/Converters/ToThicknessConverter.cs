using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class ToThicknessConverter : IMultiValueConverter {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Right { get; set; } = double.NaN;
        public double Bottom { get; set; } = double.NaN;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            var i = 0;
            return new Thickness(
                    double.IsNaN(Left) ? (i < values.Length ? values[i++].AsDouble() : 0d) : Left,
                    double.IsNaN(Top) ? (i < values.Length ? values[i++].AsDouble() : 0d) : Top,
                    double.IsNaN(Right) ? (i < values.Length ? values[i++].AsDouble() : 0d) : Right,
                    double.IsNaN(Bottom) ? (i < values.Length ? values[i].AsDouble() : 0d) : Bottom);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}