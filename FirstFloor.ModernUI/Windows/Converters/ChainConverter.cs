using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ContentProperty(nameof(Converters))]
    [ContentWrapper(typeof(ValueConverterCollection))]
    public class ChainConverter : IValueConverter {
        public ValueConverterCollection Converters { get; } = new ValueConverterCollection();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Converters
                    .Aggregate(value, (current, converter) => converter.Convert(current, targetType, parameter, culture));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Converters
                    .Reverse()
                    .Aggregate(value, (current, converter) => converter.Convert(current, targetType, parameter, culture));
        }
    }
    
    public sealed class ValueConverterCollection : Collection<IValueConverter> { }
}