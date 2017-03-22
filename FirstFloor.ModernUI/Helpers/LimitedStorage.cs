using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    using KeyValue = Tuple<string, string>;
    using KeysList = LimitedQueue<Tuple<string, string>>;

    public class LimitedStorage {
        private static readonly Dictionary<string, int> Sizes = new Dictionary<string, int>();

        public static void RegisterSpace(string name, int capacity) {
            Sizes.Add(name, capacity);
            if (_instance != null) {
                _instance._storage[name] = LoadSpace(name);
            }
        }

        private readonly Dictionary<string, KeysList> _storage;

        private LimitedStorage() {
            _storage = Sizes.ToDictionary(x => x.Key, x => LoadSpace(x.Key));
        }

        private static KeysList LoadSpace(string space) {
            return new KeysList(Sizes[space],
                    ValuesStorage.GetStringList(space)
                                 .Select(y => y.Split(new[] { '\n' }, 2))
                                 .Select(y => new KeyValue(string.IsNullOrEmpty(y[0]) ? null : Storage.Decode(y[0]), y[1])));
        }

        private static LimitedStorage _instance;

        public static LimitedStorage Instance => _instance ?? (_instance = new LimitedStorage());

        public static void Initialize() {
            Debug.Assert(_instance == null);
            _instance = new LimitedStorage();
        }

        [CanBeNull]
        public static string Get(string space, string key) {
            KeysList l;
            return _instance._storage.TryGetValue(space, out l) ? l.FirstOrDefault(x => x.Item1 == key)?.Item2 : null;
        }
        
        public static void Set(string space, string key, string value) {
            KeysList l;
            if (!_instance._storage.TryGetValue(space, out l)) {
                throw new Exception("Unsupported space: " + space);
            }

            var i = l.FindIndex(x => x.Item1 == key);
            if (i != -1) {
                if (value == null) {
                    l.RemoveAt(i);
                } else {
                    l[i] = new KeyValue(key, value);
                }
            } else if (value != null){
                l.Enqueue(new KeyValue(key, value));
            }

            _instance.Save(space);
        }
        
        public static void Move(string space, string oldKey, string newKey) {
            KeysList l;
            if (!_instance._storage.TryGetValue(space, out l)) {
                throw new Exception("Unsupported space: " + space);
            }

            var i = l.FindIndex(x => x.Item1 == oldKey);
            if (i == -1) return;

            l[i] = new KeyValue(newKey, l[i].Item2);
            _instance.Save(space);
        }

        public static void Remove(string space, string key) {
            KeysList l;
            if (!_instance._storage.TryGetValue(space, out l)) {
                throw new Exception("Unsupported space: " + space);
            }

            var i = l.FindIndex(x => x.Item1 == key);
            if (i == -1) return;
            l.RemoveAt(i);
            _instance.Save(space);
        }

        private void Save(string space) {
            KeysList l;
            if (!_storage.TryGetValue(space, out l)) {
                throw new Exception("Unsupported space: " + space);
            }

            ValuesStorage.Set(space, l.Select(x => Storage.Encode(x.Item1 ?? "") + "\n" + x.Item2));
        }
    }
}