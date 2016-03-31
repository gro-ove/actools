using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AcManager.Controls.Converters {
    public class PlaceToColorConverter : IValueConverter {
        public Brush FirstPlaceColor { get; set; }

        public Brush SecondPlaceColor { get; set; }

        public Brush ThirdPlaceColor { get; set; }

        public Brush DefaultColor { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            switch (System.Convert.ToInt32(value?.ToString() ?? "0")) {
                case 1:
                    return FirstPlaceColor;
                case 2:
                    return SecondPlaceColor;
                case 3:
                    return ThirdPlaceColor;
                default:
                    return DefaultColor;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}