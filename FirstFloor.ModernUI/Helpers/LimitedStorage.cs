using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class LimitedStorage {
        private class Space {
            private struct KeyValue {
                public string Key, Value;

                public KeyValue(string key, string value) {
                    Key = key;
                    Value = value;
                }
            }

            private readonly string _key;
            private readonly KeyValue[] _values;

            public Space(string key, int size) {
                var s = Stopwatch.StartNew();
                _key = key;
                _values = new KeyValue[size];
                var i = 0;
                foreach (var line in ValuesStorage.GetStringList(key)) {
                    var split = line.Split(new[] { '\n' }, 2);
                    if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0])) continue;
                    _values[i++] = new KeyValue(Storage.Decode(split[0]), split[1]);
                }

                if (s.Elapsed.TotalMilliseconds > 2) {
                    Logging.Debug($"{_key}, loaded {i} values : {s.Elapsed.TotalMilliseconds:F2} ms");
                }
            }

            [CanBeNull]
            public string Get(string key) {
                var l = _values;
                for (var i = l.Length - 1; i >= 0; i--) {
                    var x = l[i];
                    if (x.Key == key) return x.Value;
                }
                return null;
            }

            private static bool Find(KeyValue[] l, string key, out int index) {
                for (var i = l.Length - 1; i >= 0; i--) {
                    if (l[i].Key == key) {
                        index = i;
                        return true;
                    }
                }

                index = -1;
                return false;
            }

            public void Set(string key, string value) {
                var l = _values;
                if (Find(l, key, out var i)) {
                    l[i].Value = value;
                } else if (value != null) {
                    Array.Copy(l, 0, l, 1, l.Length - 1);
                    l[0] = new KeyValue(key, value);
                }

                Save();
            }

            public void Move(string oldKey, string newKey) {
                var l = _values;
                if (Find(l, oldKey, out var i)) {
                    l[i].Key = newKey;
                    Save();
                }
            }

            public void Remove(string key) {
                var l = _values;
                if (!Find(l, key, out var i)) return;
                Array.Copy(l, i + 1, l, i, l.Length - i - 1);
                Save();
            }

            private Busy _busy = new Busy();
            private bool _isDirty;

            public void Save() {
                _isDirty = true;
                _busy.DoDelay(SaveInner, 300);
            }

            public void ForceSave() {
                SaveInner();
            }

            private void SaveInner() {
                if (!_isDirty) return;
                _isDirty = false;

                var l = _values;
                var delimiter = Storage.Encode("\n");
                var sb = new StringBuilder(l.Length * 4);
                for (var i = 0; i < l.Length; i++) {
                    var x = l[i];
                    if (x.Key == null) break;

                    if (i > 0) sb.Append('\n');
                    sb.Append(Storage.Encode(Storage.Encode(x.Key)));
                    sb.Append(delimiter);
                    sb.Append(Storage.Encode(x.Value));
                }

                ValuesStorage.Set(_key, sb.ToString());
            }
        }

        private static readonly Dictionary<string, Space> Spaces = new Dictionary<string, Space>();

        public static void RegisterSpace(string name, int capacity) {
            if (Spaces.Count == 0) {
                Storage.Exit += OnStorageExit;
            }

            Spaces[name] = new Space(name, capacity);
        }

        private static void OnStorageExit(object s, EventArgs e) {
            foreach (var space in Spaces.Values) space.ForceSave();
        }

        [CanBeNull]
        public static string Get(string space, string key) {
            return Spaces.TryGetValue(space, out var l) ? l.Get(key) : null;
        }

        public static void Set(string space, string key, string value) {
            if (!Spaces.TryGetValue(space, out var l)) throw new Exception("Unknown space: " + space);
            l.Set(key, value);
        }

        public static void Move(string space, string oldKey, string newKey) {
            if (!Spaces.TryGetValue(space, out var l)) throw new Exception("Unknown space: " + space);
            l.Move(oldKey, newKey);
        }

        public static void Remove(string space, string key) {
            if (!Spaces.TryGetValue(space, out var l)) throw new Exception("Unknown space: " + space);
            l.Remove(key);
        }
    }
}