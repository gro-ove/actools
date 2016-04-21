using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class DictionaryExtension {
        [CanBeNull]
        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, [NotNull] TKey key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            TValue result;
            return dictionary.TryGetValue(key, out result) ? result : default(TValue);
        }
    }
}
