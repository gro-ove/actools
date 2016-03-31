using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class DictionaryExtension {
        [CanBeNull]
        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, [NotNull] TKey key) {
            return dictionary.ContainsKey(key) ? dictionary[key] : default(TValue);
        }
    }
}
