using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class Substorage : IStorage {
        private readonly IStorage _baseStorage;
        private readonly string _prefix;

        public Substorage([NotNull] IStorage baseStorage, [NotNull, LocalizationRequired(false)] string prefix) {
            _baseStorage = baseStorage;
            _prefix = prefix;
        }

        private class Enumerator : IEnumerator<KeyValuePair<string, string>> {
            private readonly IStorage _baseStorage;
            private readonly string _prefix;
            private IEnumerator<KeyValuePair<string, string>> _enumerator;
            private KeyValuePair<string, string> _current;

            public Enumerator(IStorage baseStorage, string prefix) {
                _baseStorage = baseStorage;
                _prefix = prefix;
            }

            public void Dispose() {
                if (_enumerator != null) {
                    _enumerator.Dispose();
                    _enumerator = null;
                }
            }

            public bool MoveNext() {
                if (_enumerator == null) {
                    _enumerator = _baseStorage.GetEnumerator();
                }

                KeyValuePair<string, string> current;
                do {
                    if (!_enumerator.MoveNext()) return false;
                    current = _enumerator.Current;
                } while (!current.Key.StartsWith(_prefix));

                _current = new KeyValuePair<string, string>(current.Key.Substring(_prefix.Length), current.Value);
                return true;
            }

            public void Reset() {
                if (_enumerator != null) {
                    _enumerator.Dispose();
                    _enumerator = null;
                }
            }

            public KeyValuePair<string, string> Current => _current;

            object IEnumerator.Current => _current;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() {
            return new Enumerator(_baseStorage, _prefix);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public bool Contains(string key) {
            return _baseStorage.Contains(_prefix + key);
        }

        public string GetString(string key, string defaultValue = null) {
            return _baseStorage.GetString(_prefix + key, defaultValue);
        }

        public IEnumerable<string> GetStringList(string key, IEnumerable<string> defaultValue = null) {
            return _baseStorage.GetStringList(_prefix + key, defaultValue);
        }

        public string GetEncryptedString(string key, string defaultValue = null) {
            return _baseStorage.GetEncryptedString(_prefix + key, defaultValue);
        }

        public void SetString(string key, string value) {
            _baseStorage.SetString(_prefix + key, value);
        }

        public void SetStringList(string key, IEnumerable<string> value) {
            _baseStorage.SetStringList(_prefix + key, value);
        }

        public void SetEncryptedString(string key, string value) {
            _baseStorage.SetEncryptedString(_prefix + key, value);
        }

        public bool Remove(string key) {
            return _baseStorage.Remove(_prefix + key);
        }

        public IEnumerable<string> Keys => _baseStorage.Keys.Where(x => x.StartsWith(_prefix)).Select(x => x.Substring(_prefix.Length));
    }
}