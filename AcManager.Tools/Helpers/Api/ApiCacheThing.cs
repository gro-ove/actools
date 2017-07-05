using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api {
    public class ApiCacheThing {
        private readonly TimeSpan _cacheAliveTime;
        private readonly string _directory;

        private readonly Dictionary<string, Tuple<string, DateTime>> _cache =
                new Dictionary<string, Tuple<string, DateTime>>(10);

        public ApiCacheThing(string directoryName, TimeSpan cacheAliveTime) {
            _cacheAliveTime = cacheAliveTime;
            _directory = FilesStorage.Instance.GetTemporaryDirectory(directoryName);
        }

        [NotNull]
        private static string GetTemporaryName(string argument) {
            using (var sha1 = new SHA1Managed()) {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(argument));
                return BitConverter.ToString(hash).Replace(@"-", "").ToLower();
            }
        }

        private readonly Dictionary<string, Task<string>> _tasks = new Dictionary<string, Task<string>>(5);

        [ItemCanBeNull]
        private async Task<string> GetAsyncInner([NotNull] string url, string cacheKey = null, TimeSpan? aliveTime = null,
                CancellationToken cancellation = default(CancellationToken)) {
            try {
                if (cacheKey == null) {
                    cacheKey = GetTemporaryName(url);
                }

                var actualAliveTime = aliveTime ?? _cacheAliveTime;

                Tuple<string, DateTime> cache;
                lock (_cache) {
                    cache = _cache.GetValueOrDefault(cacheKey);
                }
                if (cache != null && cache.Item2 > DateTime.Now - actualAliveTime) {
                    return cache.Item1;
                }

                var cacheFile = new FileInfo(FilesStorage.Instance.GetTemporaryFilename(_directory, cacheKey));
                if (cacheFile.Exists && cacheFile.LastWriteTime > DateTime.Now - actualAliveTime) {
                    try {
                        var data = await FileUtils.ReadAllTextAsync(cacheFile.FullName).ConfigureAwait(false);
                        lock (_cache) {
                            _cache[cacheKey] = Tuple.Create(data, cacheFile.LastWriteTime);
                        }
                        return data;
                    } catch (Exception e) {
                        Logging.Warning(e);
                        try {
                            cacheFile.Delete();
                        } catch {
                            // ignored
                        }
                    }
                }

                try {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);

                    using (var timeout = new CancellationTokenSource(KunosApiProvider.OptionWebRequestTimeout))
                    using (var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token))
                    using (var response = await HttpClientHolder.Get().SendAsync(request, combined.Token).ConfigureAwait(false)) {
                        var data = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        lock (_cache) {
                            _cache[cacheKey] = Tuple.Create(data, DateTime.Now);
                        }

                        try {
                            File.WriteAllText(cacheFile.FullName, data);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }

                        return data;
                    }
                } catch (Exception e) {
                    if (!cancellation.IsCancellationRequested) {
                        Logging.Warning(e);
                    }

                    if (cache != null) {
                        return cache.Item1;
                    }

                    if (cacheFile.Exists) {
                        var data = await FileUtils.ReadAllTextAsync(cacheFile.FullName).ConfigureAwait(false);
                        lock (_cache) {
                            _cache[cacheKey] = Tuple.Create(data, cacheFile.LastWriteTime);
                        }
                        return data;
                    }

                    return null;
                }
            } finally {
                _tasks.Remove(url);
            }
        }

        [ItemCanBeNull]
        public Task<string> GetAsync([NotNull] string url, string cacheKey = null, TimeSpan? aliveTime = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (_tasks.TryGetValue(url, out Task<string> s)) return s;
            return _tasks[url] = GetAsyncInner(url, cacheKey, aliveTime, cancellation);
        }
    }
}