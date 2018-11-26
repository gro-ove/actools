using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class DictionaryExtension {
        public static TValue GetValueOr<TKey, TValue>([NotNull] this IReadOnlyDictionary<TKey, TValue> dictionary, [NotNull] TKey key,
                [NotNull] TValue defaultValue) {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (defaultValue == null) {
                throw new ArgumentNullException(nameof(defaultValue));
            }

            return dictionary.TryGetValue(key, out var result) ? result : defaultValue;
        }

        [CanBeNull]
        public static TValue GetValueOrDefault<TKey, TValue>([NotNull] this IReadOnlyDictionary<TKey, TValue> dictionary, [NotNull, Localizable(false)]  TKey key) {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            if (key == null) throw new ArgumentNullException(nameof(key));
            return dictionary.TryGetValue(key, out var result) ? result : default;
        }

        public static void RemoveDeadReferences<TKey, TValue>([NotNull] this IDictionary<TKey, WeakReference<TValue>> dictionary) where TValue : class {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            var toRemove = dictionary.Where(x => !x.Value.TryGetTarget(out _)).Select(x => x.Key).ToList();
            foreach (var key in toRemove) {
                dictionary.Remove(key);
            }
        }

        public static WeakList<TValue> GetList<TKey, TValue>([NotNull] this IDictionary<TKey, WeakList<TValue>> dictionary, TKey key) where TValue : class {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            if (dictionary.TryGetValue(key, out var result)) {
                result.Purge();
            } else {
                result = new WeakList<TValue>(2);
                dictionary[key] = result;
            }
            return result;
        }
    }
}
