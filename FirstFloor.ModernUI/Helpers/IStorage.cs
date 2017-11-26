using System.Collections.Generic;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public interface IStorage : IEnumerable<KeyValuePair<string, string>> {
        [Pure]
        bool Contains([NotNull, LocalizationRequired(false)] string key);

        [CanBeNull, Pure]
        string GetString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null);

        [NotNull, Pure]
        IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null);

        [CanBeNull, Pure]
        string GetEncryptedString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null);

        void SetString([NotNull, LocalizationRequired(false)] string key, string value);

        void SetStringList([NotNull, LocalizationRequired(false)] string key, [NotNull] IEnumerable<string> value);

        void SetEncryptedString([NotNull, LocalizationRequired(false)] string key, string value);

        bool Remove([NotNull, LocalizationRequired(false)] string key);

        IEnumerable<string> Keys { get; }
    }
}