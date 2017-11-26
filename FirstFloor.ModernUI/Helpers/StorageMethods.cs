using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class StorageMethods {
        [Pure]
        public static byte[] GetBytes([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, byte[] defaultValue = null) {
            var v = storage.GetString(key);
            if (v == null) return defaultValue;

            try {
                return Convert.FromBase64String(v);
            } catch (Exception e) {
                Logging.Error(e.Message);
                return defaultValue;
            }
        }

        [Pure]
        public static T GetEnum<T>([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, T defaultValue = default(T)) where T : struct, IConvertible {
            return GetEnumNullable<T>(storage, key) ?? defaultValue;
        }

        [Pure]
        public static T? GetEnumNullable<T>([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) where T : struct, IConvertible {
            var value = storage.GetString(key);
            return value != null && Enum.TryParse(value, out T result) ? result : (T?)null;
        }

        [Pure]
        public static int GetInt([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, int defaultValue = 0) {
            return GetIntNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static int? GetIntNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            var value = storage.GetString(key);
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : (int?)null;
        }

        [Pure]
        public static double GetDouble([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, double defaultValue = 0) {
            return GetDoubleNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static double? GetDoubleNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            var value = storage.GetString(key);
            return value != null && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : (double?)null;
        }

        [Pure]
        public static long GetLong([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, long defaultValue = 0) {
            return GetLongNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static long? GetLongNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            var value = storage.GetString(key);
            return value != null && long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : (long?)null;
        }

        [Pure]
        public static bool GetBool([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            return GetBoolNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static bool? GetBoolNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            var value = storage.GetString(key);
            return value != null ? value == @"1" : (bool?)null;
        }

        [Pure]
        public static TimeSpan GetTimeSpan([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, TimeSpan defaultValue) {
            return storage.GetTimeSpan(key) ?? defaultValue;
        }

        [Pure]
        public static TimeSpan? GetTimeSpan([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? (TimeSpan?)null : TimeSpan.Parse(storage.GetString(key), CultureInfo.InvariantCulture);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure]
        public static DateTime GetDateTime([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, DateTime defaultValue) {
            return storage.GetDateTime(key) ?? defaultValue;
        }

        [Pure]
        public static DateTime GetDateTimeOrEpochTime([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? new DateTime(1970, 1, 1) : DateTime.Parse(storage.GetString(key), CultureInfo.InvariantCulture);
            } catch (Exception e) {
                Logging.Error(e);
                return new DateTime(1970, 1, 1);
            }
        }

        [Pure]
        public static DateTime? GetDateTime([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? (DateTime?)null : DateTime.Parse(storage.GetString(key), CultureInfo.InvariantCulture);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure, CanBeNull]
        public static TimeZoneInfo GetTimeZoneInfo([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return string.IsNullOrWhiteSpace(value) ? null : TimeZoneInfo.FromSerializedString(value);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [NotNull, Pure]
        public static Dictionary<string, string> GetDictionary([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var result = new Dictionary<string, string>();
            string k = null;
            foreach (var item in storage.GetStringList(key)) {
                if (k == null) {
                    k = item;
                } else {
                    result[k] = item;
                    k = null;
                }
            }

            return result;
        }

        [Pure, CanBeNull]
        public static Uri GetUri([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Uri defaultValue = null) {
            try {
                var value = storage.GetString(key);
                return value == null ? defaultValue : new Uri(value, UriKind.RelativeOrAbsolute);
            } catch (Exception e) {
                Logging.Error(e);
                return defaultValue;
            }
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, string value) {
            storage.SetString(key, value);
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, byte[] value) {
            storage.SetString(key, Convert.ToBase64String(value));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, IEnumerable<string> value) {
            storage.SetStringList(key, value);
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, int value) {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, double value) {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool value) {
            storage.SetString(key, value ? @"1" : @"0");
        }

        public static void SetNonDefault([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool value) {
            if (value) {
                storage.SetString(key, @"1");
            } else {
                storage.Remove(key);
            }
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, [NotNull] IReadOnlyDictionary<string, string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            storage.SetStringList(key, value.SelectMany(x => new[] { x.Key, x.Value }));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, TimeSpan timeSpan) {
            storage.SetString(key, timeSpan.ToString());
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, DateTime dateTime) {
            storage.SetString(key, dateTime.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, [NotNull] TimeZoneInfo timeZone) {
            if (timeZone == null) throw new ArgumentNullException(nameof(timeZone));
            storage.SetString(key, timeZone.ToSerializedString());
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, [NotNull] Uri uri) {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            storage.SetString(key, uri.ToString());
        }

        public static void SetEnum<T>([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, T value) where T : struct, IConvertible {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        [Pure]
        public static byte[] GetEncryptedBytes([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, byte[] defaultValue = null) {
            var v = storage.GetEncryptedString(key);
            if (v == null) return defaultValue;

            try {
                return Convert.FromBase64String(v);
            } catch (Exception e) {
                Logging.Error(e.Message);
                return defaultValue;
            }
        }

        public static bool GetEncryptedBool([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return storage.Contains(key) ? storage.GetEncryptedString(key) == @"1" : defaultValue;
        }

        public static void SetEncrypted([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, string value) {
            storage.SetEncryptedString(key, value);
        }

        public static void SetEncrypted([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, byte[] value) {
            storage.SetEncryptedString(key, Convert.ToBase64String(value));
        }

        public static void SetEncrypted([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool value) {
            storage.SetEncryptedString(key, value ? @"1" : @"0");
        }

        public static IStorage GetSubstorage([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string prefix) {
            return new Substorage(storage, prefix);
        }
    }
}