using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    // TODO: Rename to Values?
    public static partial class ValuesStorage {
        public static Storage Storage { get; private set; }

        public static void Initialize(string filename, string encryptionKey, bool disableCompression = false) {
            Debug.Assert(Storage == null);
            Storage = new Storage(filename, encryptionKey, disableCompression);
        }

        public static void Initialize() {
            Debug.Assert(Storage == null);
            Storage = new Storage();
        }

        [CanBeNull, Pure]
        public static string GetString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null) {
            return Storage.GetString(key, defaultValue);
        }

        [Pure]
        public static T GetEnum<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default(T)) where T : struct, IConvertible {
            return Storage.GetEnum(key, defaultValue);
        }

        [Pure]
        public static T? GetEnumNullable<T>([NotNull, LocalizationRequired(false)] string key) where T : struct, IConvertible {
            return Storage.GetEnumNullable<T>(key);
        }

        [Pure]
        public static int GetInt([NotNull, LocalizationRequired(false)] string key, int defaultValue = 0) {
            return Storage.GetInt(key, defaultValue);
        }

        [Pure]
        public static int? GetIntNullable([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetIntNullable(key);
        }

        [Pure]
        public static double GetDouble([NotNull, LocalizationRequired(false)] string key, double defaultValue = 0) {
            return Storage.GetDouble(key, defaultValue);
        }

        [Pure]
        public static double? GetDoubleNullable([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetDoubleNullable(key);
        }
        [Pure]
        public static Point GetPoint([NotNull, LocalizationRequired(false)] string key, Point defaultValue = default(Point)) {
            return Storage.GetPoint(key, defaultValue);
        }

        [Pure]
        public static Point? GetPointNullable([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetPointNullable(key);
        }

        [Pure]
        public static bool GetBool([NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            return Storage.GetBool(key, defaultValue);
        }

        [Pure]
        public static bool? GetBoolNullable([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetBoolNullable(key);
        }

        [NotNull, Pure]
        public static IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null) {
            return Storage.GetStringList(key, defaultValue);
        }

        [Pure]
        public static TimeSpan GetTimeSpan([NotNull, LocalizationRequired(false)] string key, TimeSpan defaultValue) {
            return Storage.GetTimeSpan(key, defaultValue);
        }

        [Pure]
        public static TimeSpan? GetTimeSpan([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetTimeSpan(key);
        }

        [Pure]
        public static DateTime GetDateTime([NotNull, LocalizationRequired(false)] string key, DateTime defaultValue) {
            return Storage.GetDateTime(key, defaultValue);
        }

        [Pure]
        public static DateTime GetDateTimeOrEpochTime([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetDateTimeOrEpochTime(key);
        }

        [Pure]
        public static DateTime? GetDateTime([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetDateTime(key);
        }

        [Pure]
        public static TimeZoneInfo GetTimeZoneInfo([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetTimeZoneInfo(key);
        }

        [Pure, CanBeNull]
        public static Uri GetUri([NotNull, LocalizationRequired(false)] string key, Uri defaultValue = null) {
            return Storage.GetUri(key, defaultValue);
        }

        [Pure]
        public static Color? GetColor([NotNull, LocalizationRequired(false)] string key) {
            return Storage.GetColor(key);
        }

        [Pure]
        public static Color GetColor([NotNull, LocalizationRequired(false)] string key, Color defaultValue) {
            return Storage.GetColor(key, defaultValue);
        }

        [Pure]
        public static bool Contains([NotNull, LocalizationRequired(false)] string key) {
            return Storage.Contains(key);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, string value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, int value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, double value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, bool value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, Point value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] IEnumerable<string> value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] IReadOnlyDictionary<string, string> value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, TimeSpan value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, DateTime value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] TimeZoneInfo value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] Uri value) {
            Storage.Set(key, value);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, Color value) {
            Storage.Set(key, value);
        }

        public static void SetEnum<T>([NotNull, LocalizationRequired(false)] string key, T value) where T : struct, IConvertible {
            Storage.SetEnum(key, value);
        }

        public static void SetEncrypted([NotNull, LocalizationRequired(false)] string key, string value) {
            Storage.SetEncrypted(key, value);
        }

        public static void SetEncrypted([NotNull, LocalizationRequired(false)] string key, bool value) {
            Storage.SetEncrypted(key, value);
        }

        public static string GetEncryptedString([NotNull, LocalizationRequired(false)] string key, [LocalizationRequired(false)] string defaultValue = null) {
            return Storage.GetEncryptedString(key, defaultValue);
        }

        public static bool GetEncryptedBool([NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            return Storage.GetEncryptedBool(key, defaultValue);
        }

        public static void Remove([NotNull, LocalizationRequired(false)] string key) {
            Storage.Remove(key);
        }
    }
}
