using System.Globalization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    public static class ConverterHelper {
        public static int AsInt([CanBeNull] this object value) {
            return value.AsInt(0);
        }

        public static int AsInt([CanBeNull] this string value) {
            return value.AsInt(0);
        }

        public static int AsInt([CanBeNull] this object value, int defaultValue) {
            return value as int? ?? value?.ToString().AsInt(defaultValue) ?? defaultValue;
        }

        public static int AsInt([CanBeNull] this string value, int defaultValue) {
            int result;
            if (value == null || !int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return defaultValue;
            }

            return result;
        }

        public static long AsLong([CanBeNull] this object value) {
            return value.AsLong(0);
        }

        public static long AsLong([CanBeNull] this string value) {
            return value.AsLong(0);
        }

        public static long AsLong([CanBeNull] this object value, long defaultValue) {
            return value as long? ?? value?.ToString().AsLong(defaultValue) ?? defaultValue;
        }

        public static long AsLong([CanBeNull] this string value, long defaultValue) {
            long result;
            if (value == null || !long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return defaultValue;
            }

            return result;
        }

        public static double AsDouble([CanBeNull] this object value) {
            return value.AsDouble(0);
        }

        public static double AsDouble([CanBeNull] this string value) {
            return value.AsDouble(0);
        }

        public static double AsDouble([CanBeNull] this object value, double defaultValue) {
            return value as double? ?? value?.ToString().AsDouble(defaultValue) ?? defaultValue;
        }

        public static double AsDouble([CanBeNull] this string value, double defaultValue) {
            double result;
            if (value == null || !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return defaultValue;
            }

            return result;
        }

        public static bool AsBoolean([CanBeNull] this object value) {
            return value.AsBoolean(false);
        }

        public static bool AsBoolean([CanBeNull] this string value) {
            return value.AsBoolean(false);
        }

        public static bool AsBoolean([CanBeNull] this object value, bool defaultValue) {
            return value as bool? ?? value?.ToString().AsBoolean(defaultValue) ?? defaultValue;
        }

        public static bool AsBoolean([CanBeNull] this string value, bool defaultValue) {
            bool result;
            if (value == null || !bool.TryParse(value, out result)) {
                return defaultValue;
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

            if (properValue is string) {
                return Equals((string)properValue, xamlValue.ToString());
            }

            return Equals(properValue, xamlValue);
        }
    }
}