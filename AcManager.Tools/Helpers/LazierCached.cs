using System;
using System.Threading.Tasks;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class LazierCached {
        [NotNull]
        public static LazierCached<T> CreateAsync<T>(string key, [CanBeNull] Func<Task<T>> fn, TimeSpan? maxAge = null) {
            return new LazierCached<T>(key, fn, maxAge);
        }
    }

    public class LazierCached<T> : Lazier<T> {
        public LazierCached(string key, Func<Task<T>> fn, TimeSpan? maxAge = null)
                : base(() => Fn(key, fn, maxAge ?? TimeSpan.MaxValue)) { }

        private static Task<T> Fn(string key, Func<Task<T>> fn, TimeSpan maxAge) {
            var timeKey = key + @".Time";

            if (DateTime.Now - CacheStorage.Get<DateTime>(timeKey) < maxAge) {
                return Task.FromResult(SimpleSerialization.IsSupported<T>()
                        ? CacheStorage.Get<T>(key)
                        : CacheStorage.Storage.GetObject<T>(key));
            }

            return fn().ContinueWith(v => {
                if (v.IsCompleted) {
                    Store(v.Result);
                    return v.Result;
                }

                if (v.IsFaulted) {
                    Logging.Warning($"Faulted: {key}, {v.Exception}");
                } else if (v.IsCanceled) {
                    Logging.Warning($"Cancelled: {key}");
                }

                Store(default(T));
                return default(T);
            });

            void Store(T value) {
                if (SimpleSerialization.IsSupported<T>()) {
                    CacheStorage.Set(key, value);
                } else {
                    CacheStorage.Storage.SetObject(key, value);
                }
            }
        }
    }
}