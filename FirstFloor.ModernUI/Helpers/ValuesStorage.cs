using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    // TODO: Rename to Values?
    public static class ValuesStorage {
        public static Storage Storage { get; private set; }

        public static void Initialize(string filename, string encryptionKey, bool disableCompression = false) {
            Debug.Assert(Storage == null);
            Storage = new Storage(filename, encryptionKey, disableCompression);
        }

        public static void Initialize() {
            Debug.Assert(Storage == null);
            Storage = new Storage();
        }

        [ContractAnnotation("defaultValue:null => canbenull; defaultValue:notnull => notnull"), Pure]
        public static T Get<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default(T)) {
            return Storage.Get(key, defaultValue);
        }

        [NotNull, Pure]
        public static IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null) {
            return Storage.GetStringList(key, defaultValue);
        }

        [Pure]
        public static bool Contains([NotNull, LocalizationRequired(false)] string key) {
            return Storage.Contains(key);
        }

        public static void Set([NotNull, LocalizationRequired(false)] string key, object value) {
            Storage.Set(key, value);
        }

        public static void SetEncrypted([NotNull, LocalizationRequired(false)] string key, object value) {
            Storage.SetEncrypted(key, value);
        }

        public static T GetEncrypted<T>([NotNull, LocalizationRequired(false)] string key, [LocalizationRequired(false)] T defaultValue = default(T)) {
            return Storage.GetEncrypted(key, defaultValue);
        }

        public static void Remove([NotNull, LocalizationRequired(false)] string key) {
            Storage.Remove(key);
        }
    }
}
