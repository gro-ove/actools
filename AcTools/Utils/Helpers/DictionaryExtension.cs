using System;
using System.Collections.Generic;
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

            TValue result;
            return dictionary.TryGetValue(key, out result) ? result : defaultValue;
        }

        [CanBeNull]
        public static TValue GetValueOrDefault<TKey, TValue>([NotNull] this IReadOnlyDictionary<TKey, TValue> dictionary, [NotNull] TKey key) {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            if (key == null) throw new ArgumentNullException(nameof(key));

            TValue result;
            return dictionary.TryGetValue(key, out result) ? result : default(TValue);
        }
        
        public static void RemoveDeadReferences<TKey, TValue>([NotNull] this IDictionary<TKey, WeakReference<TValue>> dictionary) where TValue : class {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            TValue temp;
            var toRemove = dictionary.Where(x => !x.Value.TryGetTarget(out temp)).Select(x => x.Key).ToList();

            foreach (var key in toRemove) {
                dictionary.Remove(key);
            }
        }
    }
}
