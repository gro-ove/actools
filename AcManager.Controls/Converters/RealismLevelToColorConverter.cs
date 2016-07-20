using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AcManager.Controls.ViewModels;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(RealismLevel), typeof(SolidColorBrush))]
    public class RealismLevelToColorConverter : IValueConverter {
        private static Color GetColor(RealismLevel? level) {
            switch (level) {
                case RealismLevel.Realistic:
                    return Colors.LimeGreen;

                case RealismLevel.QuiteRealistic:
                    return Colors.Yellow;

                case RealismLevel.NotQuiteRealistic:
                    return Colors.Orange;

                case RealismLevel.NonRealistic:
                    return Colors.Red;

                default:
                    return Colors.LightGray;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return new SolidColorBrush(GetColor(value as RealismLevel?));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
