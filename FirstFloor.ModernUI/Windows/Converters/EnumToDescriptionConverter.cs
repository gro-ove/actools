using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumToDescriptionConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) return null;

            var u = Nullable.GetUnderlyingType(value.GetType());
            if (u != null && u.IsEnum) {
                return GetDescription(u, System.Convert.ChangeType(value, u));
            }

            var e = value as Enum;
            return e != null ? GetDescription(e) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        private static string GetDescription(Type type, object value) {
            var name = Enum.GetName(type, value);
            if (name == null) return null;

            var field = type.GetField(name);
            var attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;

#if DEBUG
            if (attr == null) return name + @" 〈ND〉";
#else
            if (attr == null) return name;
#endif

            return attr.Description;
        }

        private static string GetDescription([NotNull] Enum value) {
            return GetDescription(value.GetType(), value);
        }
    }
}