/*using System;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.LargeFilesSharing.GoogleDrive {
    internal class InnerDataStore : IDataStore {
        private static string _prefix;

        public InnerDataStore(string prefix) {
            _prefix = prefix;
        }

        public Task StoreAsync<T>(string key, T value) {
            ValuesStorage.Set(_prefix + key, NewtonsoftJsonSerializer.Instance.Serialize(value));
            return Task.Delay(0);
        }

        public Task DeleteAsync<T>(string key) {
            ValuesStorage.Remove(_prefix + key);
            return Task.Delay(0);
        }

        public Task<T> GetAsync<T>(string key) {
            var obj = ValuesStorage.GetString(_prefix + key);
            try {
                if (obj != null) {
                    return Task.FromResult(NewtonsoftJsonSerializer.Instance.Deserialize<T>(obj));
                }
            } catch (Exception) {
                // ignored
            }
            return Task.FromResult(default(T));
        }

        public Task ClearAsync() {
            ValuesStorage.CleanUp(x => x.StartsWith(_prefix));
            return Task.Delay(0);
        }
    }
}*/