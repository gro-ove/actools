using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(int), typeof(Brush))]
    public class PlaceToColorConverter : IValueConverter {
        public Brush FirstPlaceColor { get; set; }

        public Brush SecondPlaceColor { get; set; }

        public Brush ThirdPlaceColor { get; set; }

        [CanBeNull]
        public Brush ForthPlaceColor { get; set; }

        public Brush DefaultColor { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            switch (value.AsInt()) {
                case 1:
                    return FirstPlaceColor;
                case 2:
                    return SecondPlaceColor;
                case 3:
                    return ThirdPlaceColor;
                case 4:
                    return ForthPlaceColor ?? DefaultColor;
                default:
                    return DefaultColor;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}