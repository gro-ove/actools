namespace AcTools.DataFile {
    public abstract class DataWrapperBase : IDataWrapper {
        private DataWrapperCache _cache = new DataWrapperCache();

        public abstract string Location { get; }

        public abstract bool IsEmpty { get; }

        public abstract bool IsPacked { get; }

        public T GetFile<T>(string name) where T : IDataFile, new() {
            var ret = _cache.GetFile<T>(name, out var isNewlyCreated);
            if (isNewlyCreated) {
                InitializeFile(ret, name);
            }
            return ret;
        }

        protected virtual void InitializeFile(IDataFile dataFile, string name) {
            dataFile.Initialize(this, name, null);
        }

        public abstract string GetData(string name);

        public abstract bool Contains(string name);

        protected void ClearCache() {
            _cache.Clear();
        }

        public void Refresh(string name) {
            _cache.Remove(name);
            RefreshOverride(name);
        }

        public void SetData(string name, string data, bool recycleOriginal = false) {
            _cache.Remove(name);
            SetDataOverride(name, data, recycleOriginal);
        }

        public void Delete(string name, bool recycleOriginal = false) {
            _cache.Remove(name);
            DeleteOverride(name, recycleOriginal);
        }

        protected abstract void RefreshOverride(string name);
        protected abstract void SetDataOverride(string name, string data, bool recycleOriginal);
        protected abstract void DeleteOverride(string name, bool recycleOriginal);
    }
}