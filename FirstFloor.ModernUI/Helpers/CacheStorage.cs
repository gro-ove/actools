using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class CacheStorage {
        private static string _filename;
        private static bool _disableCompression;

        private static Storage _storage;

        public static Storage Storage => _storage ?? (_storage
                = new Storage(_filename, "", _disableCompression, sizeLimit: 1000000, withoutBackups: true));

        public static void Initialize(string filename, bool disableCompression = false) {
            Debug.Assert(_storage == null);
            _filename = filename;
            _disableCompression = disableCompression;
        }

        public static void Initialize() {
            Debug.Assert(_storage == null);
            _filename = null;
            _disableCompression = false;
        }

        [ContractAnnotation("defaultValue:null => canbenull; defaultValue:notnull => notnull"), Pure]
        public static T Get<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default) {
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

        public static void Remove([NotNull, LocalizationRequired(false)] string key) {
            Storage.Remove(key);
        }
    }
}