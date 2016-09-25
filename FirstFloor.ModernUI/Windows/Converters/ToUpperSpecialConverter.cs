using System;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    /// <summary>
    /// Converts string values to upper case.
    /// </summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class ToUpperSpecialConverter : IValueConverter {
        /// <summary>
        /// Converts a value.
        /// </summary>
        /// <param name="value">The value produced by the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A converted value. If the method returns null, the valid null value is used.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            var s = value as string;
            if (s != null) {
                return s.ToUpperInvariant();
            }

            var type = value?.GetType();
            if (type != null && (type.IsValueType || type.IsGenericType)) {
                return value.ToString().ToUpperInvariant();
            }

            return value;
        }

        /// <summary>
        /// Converts a value.
        /// </summary>
        /// <param name="value">The value that is produced by the binding target.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A converted value. If the method returns null, the valid null value is used.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value;
        }
    }
}
