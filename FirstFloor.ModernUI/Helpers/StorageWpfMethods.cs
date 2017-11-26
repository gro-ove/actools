using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class StorageWpfMethods {
        [Pure]
        public static Point GetPoint([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Point defaultValue = default(Point)) {
            return GetPointNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static Point? GetPointNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? (Point?)null : Point.Parse(value);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure]
        public static Color? GetColor([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            if (!storage.Contains(key)) return null;
            try {
                var bytes = BitConverter.GetBytes(storage.GetInt(key));
                return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure]
        public static Color GetColor([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Color defaultValue) {
            return GetColor(storage, key) ?? defaultValue;
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Point value) {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Color color) {
            storage.Set(key, BitConverter.ToInt32(new[] { color.A, color.R, color.G, color.B }, 0));
        }
    }
}