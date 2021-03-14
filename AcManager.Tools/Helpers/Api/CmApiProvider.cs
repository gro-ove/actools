using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api {
    [Localizable(false)]
    public static class CmApiProvider {
        #region Initialization
        public static string UserAgent { get; private set; }
        public static string CommonUserAgent { get; }

        public static void OverrideUserAgent(string newValue) {
            UserAgent = newValue;
        }

        static CmApiProvider() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            UserAgent = $"ContentManager/{BuildInformation.AppVersion} ({windows})";
            CommonUserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.82 Safari/537.36";
        }
        #endregion

        [CanBeNull]
        public static string GetString(string url) {
            try {
                var result = InternalUtils.CmGetData_v3(url, UserAgent);
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<string> GetStringAsync(string url, CancellationToken cancellation = default) {
            try {
                var result = await InternalUtils.CmGetDataAsync(url, UserAgent, cancellation: cancellation);
                if (cancellation.IsCancellationRequested) return null;
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [CanBeNull]
        public static T Get<T>(string url) {
            try {
                var json = GetString(url);
                return json == null ? default : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning(e);
                return default;
            }
        }

        [ItemCanBeNull]
        public static async Task<T> GetAsync<T>(string url, CancellationToken cancellation = default) {
            try {
                var json = await GetStringAsync(url, cancellation);
                if (cancellation.IsCancellationRequested) return default;
                return json == null ? default : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning(e);
                return default;
            }
        }

        [CanBeNull]
        public static byte[] GetData(string url) {
            return InternalUtils.CmGetData_v3(url, UserAgent);
        }

        [ItemCanBeNull]
        public static Task<byte[]> GetDataAsync(string url, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            return InternalUtils.CmGetDataAsync(url, UserAgent, progress, cancellation);
        }

        private static readonly List<string> JustLoadedStaticData = new List<string>();

        /// <summary>
        /// Load piece of static data, either from CM API, or from cache.
        /// </summary>
        /// <returns>Cached filename and if data is just loaded or not.</returns>
        [ItemCanBeNull]
        public static async Task<Tuple<string, bool>> GetStaticDataAsync([NotNull] string id, TimeSpan maxAge, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var file = new FileInfo(FilesStorage.Instance.GetFilename("Static", $"{id}.zip"));

            if (file.Exists && (JustLoadedStaticData.Contains(id) || DateTime.Now - file.LastWriteTime < maxAge)) {
                return Tuple.Create(file.FullName, false);
            }

            var result = await InternalUtils.CmGetDataAsync($"static/get/{id}", UserAgent,
                    file.Exists ? file.LastWriteTime : (DateTime?)null, progress, cancellation).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return null;

            if (result != null && result.Item1.Length != 0) {
                Logging.Write($"Fresh version of {id} loaded, from {result.Item2?.ToString() ?? "UNKNOWN"}");
                var lastWriteTime = result.Item2 ?? DateTime.Now;
                await FileUtils.WriteAllBytesAsync(file.FullName, result.Item1, cancellation).ConfigureAwait(false);
                file.Refresh();
                file.LastWriteTime = lastWriteTime;
                JustLoadedStaticData.Add(id);
                return Tuple.Create(file.FullName, true);
            }

            if (!file.Exists) {
                return null;
            }

            Logging.Write($"Cached {id} used");
            JustLoadedStaticData.Add(id);
            return Tuple.Create(file.FullName, false);
        }

        private static readonly List<string> JustLoadedPaintShopData = new List<string>();

        /// <summary>
        /// Load piece of data for Paint Shop, either from CM API, or from cache.
        /// </summary>
        /// <param name="carId"></param>
        /// <param name="progress"></param>
        /// <param name="cancellation"></param>
        /// <returns>Cached filename and if data is just loaded or not.</returns>
        [ItemCanBeNull]
        public static async Task<Tuple<string, bool>> GetPaintShopDataAsync([NotNull] string carId, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var file = new FileInfo(FilesStorage.Instance.GetTemporaryFilename("Paint Shop", $"{carId}.zip"));

            if (JustLoadedPaintShopData.Contains(carId) && file.Exists) {
                return Tuple.Create(file.FullName, false);
            }

            var result = await InternalUtils.CmGetPaintShopDataAsync(carId, UserAgent,
                    file.Exists ? file.LastWriteTime : (DateTime?)null, progress, cancellation).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return null;

            if (result != null && result.Item1.Length != 0) {
                Logging.Debug($"Fresh version of {carId} loaded, from {result.Item2?.ToString() ?? "UNKNOWN"}");
                var lastWriteTime = result.Item2 ?? DateTime.Now;
                await FileUtils.WriteAllBytesAsync(file.FullName, result.Item1, cancellation).ConfigureAwait(false);
                file.Refresh();
                file.LastWriteTime = lastWriteTime;
                JustLoadedPaintShopData.Add(carId);
                return Tuple.Create(file.FullName, true);
            }

            if (!file.Exists) {
                return null;
            }

            Logging.Debug($"Cached {carId} used");
            JustLoadedPaintShopData.Add(carId);
            return Tuple.Create(file.FullName, false);
        }

        private static string[] _paintShopIds;

        /// <summary>
        /// Load piece of data for Paint Shop, either from CM API, or from cache.
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="cancellation"></param>
        /// <returns>Cached filename and if data is just loaded or not.</returns>
        [ItemCanBeNull]
        public static async Task<string[]> GetPaintShopIdsAsync(IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            if (_paintShopIds != null) {
                return _paintShopIds;
            }

            var file = new FileInfo(FilesStorage.Instance.GetTemporaryFilename("Paint Shop", "IDs.json"));
            var result = await InternalUtils.CmGetPaintShopDataAsync(null, UserAgent,
                    file.Exists ? file.LastWriteTime : (DateTime?)null, progress, cancellation).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return null;

            if (result != null && result.Item1.Length != 0) {
                Logging.Debug($"Fresh version of IDs loaded, from {result.Item2?.ToString() ?? "UNKNOWN"}");
                var lastWriteTime = result.Item2 ?? DateTime.Now;
                await FileUtils.WriteAllBytesAsync(file.FullName, result.Item1, cancellation).ConfigureAwait(false);
                file.Refresh();
                file.LastWriteTime = lastWriteTime;
                _paintShopIds = JsonConvert.DeserializeObject<string[]>(result.Item1.ToUtf8String());
                return _paintShopIds;
            }

            if (!file.Exists) {
                return null;
            }

            Logging.Debug("Cached IDs used");
            _paintShopIds = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(file.FullName));
            return _paintShopIds;
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetStaticDataBytesAsync(string id, TimeSpan maxAge, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var t = await GetStaticDataAsync(id, maxAge, progress, cancellation);
            return t == null ? null : await FileUtils.ReadAllBytesAsync(t.Item1);
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetStaticDataBytesIfUpdatedAsync(string id, TimeSpan maxAge, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var t = await GetStaticDataAsync(id, maxAge, progress, cancellation);
            return t?.Item2 == true ? await FileUtils.ReadAllBytesAsync(t.Item1) : null;
        }

        private static readonly List<string> JustLoadedPatchData = new List<string>();

        public static void ResetPatchDataCache(PatchDataType type, [NotNull] string version) {
            var key = GetPatchCacheKey(type, version);
            var file = FilesStorage.Instance.GetFilename("Temporary", "Patch", key);
            JustLoadedPatchData.Remove(key);
            FileUtils.TryToDelete(file);
        }

        public enum PatchDataType {
            Manifest, Patch, Chunk
        }

        /// <summary>
        /// Load piece of static data, either from CM API, or from cache.
        /// </summary>
        /// <returns>Cached filename and if data is just loaded or not.</returns>
        [ItemCanBeNull]
        public static async Task<Tuple<string, bool>> GetPatchDataAsync(PatchDataType type, [NotNull] string version, TimeSpan maxAge, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var key = GetPatchCacheKey(type, version);
            var file = new FileInfo(FilesStorage.Instance.GetFilename("Temporary", "Patch", key));

            if (file.Exists && (JustLoadedPatchData.Contains(key) || DateTime.Now - file.LastWriteTime < maxAge)) {
                return Tuple.Create(file.FullName, false);
            }

            var result = await InternalUtils.CmGetDataAsync(GetPatchUrl(type, version), UserAgent,
                    file.Exists ? file.LastWriteTime : (DateTime?)null, progress, cancellation).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return null;

            if (result != null && result.Item1.Length != 0) {
                Logging.Write($"Fresh version of {key} loaded, from {result.Item2?.ToString() ?? "UNKNOWN"}");
                var lastWriteTime = result.Item2 ?? DateTime.Now;
                await FileUtils.WriteAllBytesAsync(file.FullName, result.Item1, cancellation).ConfigureAwait(false);
                file.Refresh();
                file.LastWriteTime = lastWriteTime;
                JustLoadedPatchData.Add(key);
                return Tuple.Create(file.FullName, true);
            }

            if (!file.Exists) {
                return null;
            }

            Logging.Write($"Cached {key} used");
            JustLoadedPatchData.Add(key);
            return Tuple.Create(file.FullName, false);
        }

        private static string GetPatchCacheKey(PatchDataType type, string version) {
            switch (type) {
                case PatchDataType.Manifest:
                    return "Manifest.json";
                case PatchDataType.Patch:
                    return $"patch-{version}.zip";
                case PatchDataType.Chunk:
                    return $"chunk-{version}.zip";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static string GetPatchUrl(PatchDataType type, string version) {
            switch (type) {
                case PatchDataType.Manifest:
                    return "patch/manifest";
                case PatchDataType.Patch:
                    return $"patch/get/{version}";
                case PatchDataType.Chunk:
                    return $"patch/chunk/{version}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetPatchVersionAsync(string version, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var t = await GetPatchDataAsync(PatchDataType.Patch, version, TimeSpan.MaxValue, progress, cancellation);
            try {
                return t == null ? null : await FileUtils.ReadAllBytesAsync(t.Item1);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetChunkVersionAsync(string version, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            var t = await GetPatchDataAsync(PatchDataType.Chunk, version, TimeSpan.MaxValue, progress, cancellation);
            try {
                return t == null ? null : await FileUtils.ReadAllBytesAsync(t.Item1);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        public static bool HasPatchCached(string version) {
            return File.Exists(FilesStorage.Instance.GetFilename("Temporary", "Patch", $"{version}.zip"));
        }

        [ItemCanBeNull]
        public static async Task<string> GetContentStringAsync(string url, CancellationToken cancellation = default) {
            try {
                var result = await InternalUtils.CmGetContentDataAsync(url, UserAgent, null, cancellation);
                if (cancellation.IsCancellationRequested) return null;
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<T> GetContentAsync<T>(string url = "", CancellationToken cancellation = default) {
            try {
                var json = await GetContentStringAsync(url, cancellation);
                if (cancellation.IsCancellationRequested) return default;
                return json == null ? default : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                Logging.Warning(e);
            }
            return default;
        }

        [ItemCanBeNull]
        public static async Task<string> PostOnlineDataAsync(ServerInformationExtra data, CancellationToken cancellation = default) {
            try {
                var json = JsonConvert.SerializeObject(data);
                var key = ".PostedOnlineData:" + json.GetChecksum();
                var uploaded = CacheStorage.Get<string>(key);
                if (uploaded != null) return uploaded;

                var id = await InternalUtils.PostOnlineDataAsync(json, UserAgent, cancellation);
                if (id != null) {
                    CacheStorage.Set(key, id);
                    LazierCached.Set(@".OnlineData:" + id, data);
                }

                return id;
            } catch (Exception e) when (e.IsCancelled()) { } catch (WebException e) when (e.Response is HttpWebResponse h) {
                try {
                    var s = h.GetResponseStream()?.ReadAsStringAndDispose();
                    if (s != null) {
                        var o = JObject.Parse(s);
                        if (o["error"] != null) {
                            NonfatalError.NotifyBackground($"Can’t share online data: {o["error"].ToString().ToSentenceMember()}",
                                    o["details"]?.ToString().ToSentence());
                            return null;
                        }
                    }
                    NonfatalError.NotifyBackground($"Can’t share online data: {h.StatusDescription.ToLower()}", e);
                } catch (Exception ex) {
                    Logging.Warning(ex);
                    NonfatalError.NotifyBackground("Can’t share online data", e);
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t share online data", e);
            }
            return null;
        }

        [ItemCanBeNull]
        public static Task<ServerInformationExtra> GetOnlineDataAsync(string id, CancellationToken cancellation = default) {
            return LazierCached.CreateAsync(@".OnlineData:" + id,
                    () => InternalUtils.GetOnlineDataAsync(id, UserAgent, cancellation).ContinueWith(
                            r => {
                                // Logging.Debug(JsonConvert.SerializeObject(r.Result));
                                return JsonConvert.DeserializeObject<ServerInformationExtra>(r.Result);
                            },
                            TaskContinuationOptions.OnlyOnRanToCompletion)
                    ).GetValueAsync();
        }
    }
}