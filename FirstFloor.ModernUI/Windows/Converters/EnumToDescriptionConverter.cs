using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumToDescriptionConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as Enum)?.GetDescription();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    internal static class EnumExtension {
        public static string GetDescription([NotNull] this Enum value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            if (name == null) return null;

            var field = type.GetField(name);
            if (field == null) return null;
            var attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attr?.Description;
        }
    }
}