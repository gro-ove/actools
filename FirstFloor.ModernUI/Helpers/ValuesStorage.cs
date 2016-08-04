using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static partial class ValuesStorage {
        private static Storage _storage;

        public static Storage Instance => _storage ?? (_storage = new Storage(encryptionKey: EncryptionKey));

        public static void Initialize(string filename, bool disableCompression = false) {
            Debug.Assert(_storage == null);
            _storage = new Storage(filename, EncryptionKey, disableCompression);
        }

        [CanBeNull, Pure]
        public static string GetString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null) {
            return _storage.GetString(key, defaultValue);
        }

        [Pure]
        public static T GetEnum<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default(T)) where T : struct, IConvertible {
            return _storage.GetEnum(key, defaultValue);
        }

        [Pure]
        public static T? GetEnumNullable<T>([NotNull, LocalizationRequired(false)] string key) where T : struct, IConvertible {
            return _storage.GetEnumNullable<T>(key);
        }

        [Pure]
        public static int GetInt([NotNull, LocalizationRequired(false)] string key, int defaultValue = 0) {
            return _storage.GetInt(key, defaultValue);
        }

        [Pure]
        public static int? GetIntNullable([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetIntNullable(key);
        }

        [Pure]
        public static double GetDouble([NotNull, LocalizationRequired(false)] string key, double defaultValue = 0) {
            return _storage.GetDouble(key, defaultValue);
        }

        [Pure]
        public static double? GetDoubleNullable([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetDoubleNullable(key);
        }
        [Pure]
        public static Point GetPoint([NotNull, LocalizationRequired(false)] string key, Point defaultValue = default(Point)) {
            return _storage.GetPoint(key, defaultValue);
        }

        [Pure]
        public static Point? GetPointNullable([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetPointNullable(key);
        }

        [Pure]
        public static bool GetBool([NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            return _storage.GetBool(key, defaultValue);
        }

        [Pure]
        public static bool? GetBoolNullable([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetBoolNullable(key);
        }

        [NotNull, Pure]
        public static IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null) {
            return _storage.GetStringList(key, defaultValue);
        }

        [Pure]
        public static TimeSpan GetTimeSpan([NotNull, LocalizationRequired(false)] string key, TimeSpan defaultValue) {
            return _storage.GetTimeSpan(key, defaultValue);
        }

        [Pure]
        public static TimeSpan? GetTimeSpan([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetTimeSpan(key);
        }

        [Pure]
        public static DateTime GetDateTime([NotNull, LocalizationRequired(false)] string key, DateTime defaultValue) {
            return _storage.GetDateTime(key, defaultValue);
        }

        [Pure]
        public static DateTime GetDateTimeOrEpochTime([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetDateTimeOrEpochTime(key);
        }

        [Pure]
        public static DateTime? GetDateTime([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetDateTime(key);
        }
        [Pure]
        public static TimeZoneInfo GetTimeZoneInfo([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetTimeZoneInfo(key);
        }

        [Pure, CanBeNull]
        public static Uri GetUri([NotNull, LocalizationRequired(false)] string key, Uri defaultValue = null) {
            return _storage.GetUri(key, defaultValue);
        }

        [Pure]
        public static Color? GetColor([NotNull, LocalizationRequired(false)] string key) {
            return _storage.GetColor(key);
        }
        [Pure]
        public static Color GetColor([NotNull, LocalizationRequired(false)] string key, Color defaultValue) {
            return _storage.GetColor(key, defaultValue);
        }

        [Pure]
        public static bool Contains([NotNull, LocalizationRequired(false)] string key) {
            return _storage.Contains(key);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, string value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, int value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, double value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, bool value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, Point value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] IEnumerable<string> value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] IReadOnlyDictionary<string, string> value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, TimeSpan value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, DateTime value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] TimeZoneInfo value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] Uri value) {
            _storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, Color value) {
            _storage.Set(key, value);
        }

        public static void SetEnum<T>([NotNull, LocalizationRequired(false)] string key, T value) where T : struct, IConvertible {
            _storage.SetEnum(key, value);
        }

        public static void SetEncrypted([NotNull, LocalizationRequired(false)] string key, string value) {
            _storage.SetEncrypted(key, value);
        }

        public static void SetEncrypted([NotNull, LocalizationRequired(false)] string key, bool value) {
            _storage.SetEncrypted(key, value);
        }

    }
}
