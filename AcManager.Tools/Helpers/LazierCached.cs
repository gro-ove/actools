using System;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class LazierCached {
        internal const string Postfix = @".CachedTime";

        public static void Purge() {
            var s = CacheStorage.Storage;
            var n = DateTime.Now;
            var i = 0;
            foreach (var key in s.Keys.Where(x => x.EndsWith(Postfix) && (n - s.Get<DateTime>(x)).TotalDays > 10).ToList()) {
                s.Remove(key);
                s.Remove(key.ApartFromLast(Postfix));
                i++;
            }

            if (i > 0) {
                Logging.Write($"Removed old entries: {i}");
            }
        }

        public static void Set<T>(string key, T value) {
            CacheStorage.Set(key + Postfix, DateTime.Now);
            if (SimpleSerialization.IsSupported<T>()) {
                CacheStorage.Set(key, value);
            } else {
                CacheStorage.Storage.SetObject(key, value);
            }
        }

        [NotNull]
        public static LazierCached<T> CreateAsync<T>(string key, [CanBeNull] Func<Task<T>> fn, TimeSpan? maxAge = null) {
            return new LazierCached<T>(key, fn, maxAge);
        }
    }

    public class LazierCached<T> : Lazier<T> {
        public LazierCached(string key, Func<Task<T>> fn, TimeSpan? maxAge = null)
                : base(() => Fn(key, fn, maxAge ?? TimeSpan.MaxValue)) { }

        private static Task<T> Fn(string key, Func<Task<T>> fn, TimeSpan maxAge) {
            var timeKey = key + LazierCached.Postfix;

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
                CacheStorage.Set(timeKey, DateTime.Now);
                if (SimpleSerialization.IsSupported<T>()) {
                    CacheStorage.Set(key, value);
                } else {
                    CacheStorage.Storage.SetObject(key, value);
                }
            }
        }
    }
}