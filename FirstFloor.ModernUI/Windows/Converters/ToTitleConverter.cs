using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

// Localize me!
namespace FirstFloor.ModernUI.Windows.Converters {
    /// <summary>
    /// Converts string values to upper case.
    /// </summary>
    public class ToTitleConverter : IValueConverter {
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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null || culture.Name == "ru-RU") return value;
            return string.Join(" ", value.ToString()
                                         .Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(x => x.Substring(0, 1).ToUpper(CultureInfo.CurrentCulture) + (x.Length == 1 ? "" : x.Substring(1))));
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
