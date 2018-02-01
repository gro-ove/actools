using System;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools {
    public static class StorageObjectExtension {
        [CanBeNull]
        public static T GetObject<T>(this IStorage storage, string key) {
            var json = storage.Get<string>(key);
            try {
                if (!string.IsNullOrWhiteSpace(json)) {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            return default(T);
        }

        public static T GetOrCreateObject<T>(this IStorage storage, string key) where T : new() {
            var json = storage.Get<string>(key);
            try {
                if (!string.IsNullOrWhiteSpace(json)) {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            return new T();
        }

        public static void SetObject(this IStorage storage, [NotNull] string key, object value) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            storage.Set(key, value == null ? null : JsonConvert.SerializeObject(value));
        }
    }
}