using System.Globalization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    public static class ConverterHelper {
        public static int AsInt([CanBeNull] this object value) {
            return value as int? ?? value?.ToString().AsInt() ?? 0;
        }

        public static int AsInt([CanBeNull] this string value) {
            int result;
            if (value == null || !int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return 0;
            }

            return result;
        }

        public static double AsDouble([CanBeNull] this object value) {
            return value as double? ?? value?.ToString().AsDouble() ?? 0d;
        }

        public static double AsDouble([CanBeNull] this string value) {
            double result;
            if (value == null || !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return 0d;
            }

            return result;
        }

        public static bool AsBoolean([CanBeNull] this object value) {
            return value as bool? ?? value?.ToString().AsBoolean() ?? false;
        }

        public static bool AsBoolean([CanBeNull] this string value) {
            bool result;
            if (value == null || !bool.TryParse(value, out result)) {
                return false;
            }

            return result;
        }

        public static bool XamlEquals([CanBeNull] this object properValue, [CanBeNull] object xamlValue) {
            if (properValue == null) return xamlValue == null;
            if (xamlValue == null) return false;

            if (properValue is double) {
                return Equals((double)properValue, xamlValue.AsDouble());
            }

            if (properValue is int) {
                return Equals((int)properValue, xamlValue.AsInt());
            }

            if (properValue is bool) {
                return Equals((bool)properValue, xamlValue.AsBoolean());
            }

            return Equals(properValue, xamlValue);
        }
    }
}