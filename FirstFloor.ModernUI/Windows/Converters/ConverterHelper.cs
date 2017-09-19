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
            return value == null || !int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? defaultValue : result;
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
            return value == null || !long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? defaultValue : result;
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
            return value == null || !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? defaultValue : result;
        }

        public static float AsFloat([CanBeNull] this object value) {
            return value.AsFloat(0);
        }

        public static float AsFloat([CanBeNull] this string value) {
            return value.AsFloat(0);
        }

        public static float AsFloat([CanBeNull] this object value, float defaultValue) {
            return value as float? ?? value?.ToString().AsFloat(defaultValue) ?? defaultValue;
        }

        public static float AsFloat([CanBeNull] this string value, float defaultValue) {
            return value == null || !float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? defaultValue : result;
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
            return value == null || !bool.TryParse(value, out var result) ? defaultValue : result;
        }

        public static bool XamlEquals([CanBeNull] this object properValue, [CanBeNull] object xamlValue) {
            if (properValue == null) return xamlValue == null;
            if (xamlValue == null) return false;

            switch (properValue) {
                case double d:
                    return Equals(d, xamlValue.AsDouble());
                case int i:
                    return Equals(i, xamlValue.AsInt());
                case bool b:
                    return Equals(b, xamlValue.AsBoolean());
                case string s:
                    return Equals(s, xamlValue.ToString());
            }

            return Equals(properValue, xamlValue);
        }
    }
}