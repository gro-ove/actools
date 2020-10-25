using System.Collections.Generic;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Pages.Workshop {
    public class VirtualDataWrapper : IDataReadWrapper {
        public Dictionary<string, string> Data { get; } = new Dictionary<string, string>();

        private DataWrapperCache _cache = new DataWrapperCache();

        public T GetFile<T>(string name) where T : IDataFile, new() {
            var ret = _cache.GetFile<T>(name, out var isNewlyCreated);
            if (isNewlyCreated) {
                ret.Initialize(this, name, null);
            }
            return ret;
        }

        public bool IsEmpty => Data.Count == 0;

        public bool IsPacked => true;

        public string GetData(string name) {
            return Data.GetValueOrDefault(name);
        }
    }
}