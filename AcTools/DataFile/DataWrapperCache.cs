using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class DataWrapperCache {
        private object _cacheLock = new object();

        [CanBeNull]
        private Dictionary<string, IDataFile> _cache;

        public T GetFile<T>(string name, out bool isNewlyCreated) where T : IDataFile, new() {
            lock (_cacheLock) {
                if (_cache == null) {
                    _cache = new Dictionary<string, IDataFile>();
                }

                if (_cache.TryGetValue(name, out var v) && v is T file) {
                    isNewlyCreated = false;
                    return file;
                }
            }

            var t = new T();
            lock (_cacheLock) {
                _cache[name] = t;
            }

            isNewlyCreated = true;
            return t;
        }

        public void Clear() {
            if (_cache == null) return;
            lock (_cacheLock) {
                _cache.Clear();
            }
        }

        public void Remove([CanBeNull] string name) {
            if (_cache != null) {
                lock (_cacheLock) {
                    if (name == null) {
                        _cache.Clear();
                    } else if (_cache.ContainsKey(name)) {
                        _cache.Remove(name);
                    }
                }
            }
        }
    }
}