using System;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Profile {
    public static class StorageObjectExtension {
        public static T GetOrCreateObject<T>(this Storage storage, string key) where T : new() {
            var json = storage.GetString(key);
            try {
                if (!string.IsNullOrWhiteSpace(json)) {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            return new T();
        }

        public static T GetOrCreateObject<T>(this Storage storage, string key, Func<T> creation){
            var json = storage.GetString(key);
            try {
                if (!string.IsNullOrWhiteSpace(json)) {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            return creation();
        }

        public static void Set(this Storage storage, [NotNull] string key, object value) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            storage.Set(key, value == null ? null : JsonConvert.SerializeObject(value));
        }
    }
}