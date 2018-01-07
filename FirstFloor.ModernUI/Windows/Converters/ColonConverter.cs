using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    /// <summary>
    /// Because I’ve leant too late about French language.
    /// </summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class ColonConverter : IValueConverter {
        public static string Format => UiStrings.ValueLabel_Format;
        public static string FormatBoth => UiStrings.ValueLabel_Format + @"{1}";
        public static string FormatNoSpaceAfterwards => Format.TrimEnd();
        public static string Colon => string.Format(FormatNoSpaceAfterwards, "");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            switch (parameter) {
                case "trim":
                    return string.Format(Format, value).Trim();
                default:
                    return string.Format(Format, value);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value;
        }
    }
}