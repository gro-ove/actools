using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public abstract class DataWrapperBase : IDataWrapper {
        private object _cacheLock = new object();

        [CanBeNull]
        private Dictionary<string, IDataFile> _cache;

        public abstract string Location { get; }

        public T GetFile<T>(string name) where T : IDataFile, new() {
            lock (_cacheLock) {
                if (_cache == null) {
                    _cache = new Dictionary<string, IDataFile>();
                }

                if (_cache.TryGetValue(name, out var v) && v is T) {
                    return (T)v;
                }
            }

            var t = new T();
            lock (_cacheLock) {
                _cache[name] = t;
            }

            InitializeFile(t, name);
            return t;
        }

        protected virtual void InitializeFile(IDataFile dataFile, string name) {
            dataFile.Initialize(this, name, null);
        }

        public abstract string GetData(string name);
        public abstract bool Contains(string name);

        protected void ClearCache() {
            if (_cache == null) return;
            lock (_cacheLock) {
                _cache.Clear();
            }
        }

        public void Refresh(string name) {
            if (_cache != null) {
                lock (_cacheLock) {
                    if (name == null) {
                        _cache.Clear();
                    } else if (_cache.ContainsKey(name)) {
                        _cache.Remove(name);
                    }
                }
            }

            RefreshOverride(name);
        }

        public void SetData(string name, string data, bool recycleOriginal = false) {
            if (_cache != null) {
                lock (_cacheLock) {
                    _cache.Remove(name);
                }
            }

            SetDataOverride(name, data, recycleOriginal);
        }

        public void Delete(string name, bool recycleOriginal = false) {
            if (_cache != null) {
                lock (_cacheLock) {
                    _cache.Remove(name);
                }
            }

            DeleteOverride(name, recycleOriginal);
        }

        protected abstract void RefreshOverride(string name);
        protected abstract void SetDataOverride(string name, string data, bool recycleOriginal);
        protected abstract void DeleteOverride(string name, bool recycleOriginal);
    }
}