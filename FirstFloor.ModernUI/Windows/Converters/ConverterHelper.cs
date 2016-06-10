using System.Globalization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    public static class ConverterHelper {
        public static int AsInt([CanBeNull] this object value, int defaultValue) {
            if (value is int) {
                return (int)value;
            }

            int result;
            if (value == null || !int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return defaultValue;
            }

            return result;
        }

        public static int AsInt([CanBeNull] this object value) {
            if (value is int) {
                return (int)value;
            }

            int result;
            if (value == null || !int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return 0;
            }

            return result;
        }

        public static double AsDouble([CanBeNull] this object value, double defaultValue) {
            if (value is double) {
                return (double)value;
            }

            double result;
            if (value == null || !double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return defaultValue;
            }

            return result;
        }

        public static double AsDouble([CanBeNull] this object value) {
            if (value is double) {
                return (double)value;
            }

            double result;
            if (value == null || !double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result)) {
                return 0d;
            }

            return result;
        }
    }
}