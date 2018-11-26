using System.Collections.Generic;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public interface IStorage : IEnumerable<KeyValuePair<string, string>> {
        [Pure]
        bool Contains([NotNull, LocalizationRequired(false)] string key);

        [CanBeNull, Pure]
        T Get<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default);

        [NotNull, Pure]
        IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null);

        [CanBeNull, Pure]
        T GetEncrypted<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default);

        void Set([NotNull, LocalizationRequired(false)] string key, object value);

        void SetStringList([NotNull, LocalizationRequired(false)] string key, [NotNull] IEnumerable<string> value);

        void SetEncrypted([NotNull, LocalizationRequired(false)] string key, object value);

        bool Remove([NotNull, LocalizationRequired(false)] string key);

        [NotNull]
        IEnumerable<string> Keys { get; }

        void Clear();
    }
}