using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Workshop.Data;
using AcManager.Workshop.Uploaders;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop {
    public class WorkshopClient : NotifyPropertyChanged {
        public static bool OptionUserAvailable = false;
        public static bool OptionCreatorAvailable = false;

        private const string Salt1 = "Mghf4ZTzPQFGYJMA";
        private const string Salt2 = "5qzAfOyqBIgNHG0V";
        private const string Salt3 = "h3y2zIonOlgf1AQS";
        private const string Salt4 = "5wXRea0U5wXRea0U";
        private const string Salt5 = "IARgcb0Jk3mksyfeQl3xUqHJnEKLmi8f";

        [NotNull]
        private readonly string _apiHost;

        [CanBeNull]
        private string _userId;

        [CanBeNull]
        public string UserId {
            get => _userId;
            set {
                _userId = value;
                _pokeKey = null;
            }
        }

        [CanBeNull]
        private string _userPasswordChecksum;

        [CanBeNull]
        public string UserPasswordChecksum {
            get => _userPasswordChecksum;
            set {
                _userPasswordChecksum = value;
                _pokeKey = null;
            }
        }

        private string _pokeKey;

        public WorkshopClient([NotNull] string apiHost) {
            _apiHost = apiHost;
        }

        #region Basic stuff for sending requests
        [NotNull]
        private string GetFullUrl([NotNull] string path) {
            return $@"{_apiHost}/v1{path}";
        }

        public static string GetUserId([NotNull] string steamId) {
            if (steamId == null) throw new ArgumentNullException(nameof(steamId));
            using (var sha = SHA256.Create()) {
                return Regex.Replace(Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Salt4 + steamId))), @"\W+|=+$", "").Substring(0, 16);
            }
        }

        public static string GetPasswordChecksum([NotNull] string userId, [NotNull] string userPassword) {
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            if (userPassword == null) throw new ArgumentNullException(nameof(userPassword));
            // Logging.Debug($"userId={userId}, userPassword=`{userPassword}`, checksum={GetChecksum(GetChecksum(Salt3 + userId) + userPassword)}");
            return GetChecksum(GetChecksum(Salt3 + userId) + userPassword);
        }

        private static string ToHexString([NotNull] byte[] data) {
            const string lookup = "0123456789abcdef";
            int i = -1, p = -1, l = data.Length;
            var c = new char[l-- * 2];
            while (i < l) {
                var d = data[++i];
                c[++p] = lookup[d >> 4];
                c[++p] = lookup[d & 0xF];
            }
            return new string(c, 0, c.Length);
        }

        [NotNull]
        private static string GetChecksum([NotNull] string s) {
            using (var sha = SHA256.Create()) {
                return ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
            }
        }

        private static ConfiguredTaskAwaitable<byte[]> LoadBinaryContent(HttpResponseMessage response, CancellationToken cancellation) {
            return response.Content.ReadAsByteArrayAsync().WithCancellation(cancellation).ConfigureAwait(false);
        }

        private static ConfiguredTaskAwaitable<string> LoadContent(HttpResponseMessage response, CancellationToken cancellation) {
            return response.Content.ReadAsStringAsync().WithCancellation(cancellation).ConfigureAwait(false);
        }

        private static async Task TestResponse(string url, HttpResponseMessage response, CancellationToken cancellation) {
            if (response.StatusCode >= (HttpStatusCode)400) {
                var phrase = response.StatusCode == HttpStatusCode.NotFound ? $"Not found: {url}" : response.ReasonPhrase;
                try {
                    var content = await LoadContent(response, cancellation);
                    if (string.IsNullOrEmpty(content)) {
                        throw new WorkshopHttpException(response.StatusCode, phrase);
                    }

                    var details = JObject.Parse(content);
                    var errorMessage = details["error"].ToString();
                    var exception = details["exception"];
                    string remoteException = null;
                    string[] remoteStackTrace = null;
                    if (exception != null) {
                        remoteException = exception["message"].ToString();
                        remoteStackTrace = exception["stack"]?.ToObject<string[]>();
                        if (remoteStackTrace?.Length == 0) {
                            remoteStackTrace = null;
                        }
                    }
                    Logging.Debug("Managed to process: " + response.StatusCode);
                    throw new WorkshopException(response.StatusCode, errorMessage, remoteException, remoteStackTrace);
                } catch (Exception e) when (!(e is WorkshopException)) {
                    Logging.Warning(e);
                    throw new WorkshopHttpException(response.StatusCode, phrase);
                }
            }
        }

        [ItemNotNull]
        private async Task<string> PokeAsync(CancellationToken cancellation) {
            var request = new HttpRequestMessage(HttpMethod.Post, GetFullUrl($"/users/{_userId}/poke/{GetChecksum(Salt1 + UserId)}"));
            try {
                using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                    await TestResponse("<poke path>", response, cancellation).ConfigureAwait(false);
                    return JObject.Parse(await response.Content.ReadAsStringAsync().WithCancellation(cancellation).ConfigureAwait(false))["poke"].ToString();
                }
            } catch (Exception e) when (e.IsCancelled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }

        [ItemNotNull]
        private async Task<HttpRequestMessage> CreateHttpRequestAsync([NotNull] HttpMethod method, [NotNull] string url, [CanBeNull] string data,
                CancellationToken cancellation) {
            var request = new HttpRequestMessage(method, GetFullUrl(url));
            if (UserId != null && UserPasswordChecksum != null) {
                var poke = _pokeKey ?? (_pokeKey = await PokeAsync(cancellation).ConfigureAwait(false));
                var randomLine = GetChecksum(Guid.NewGuid().ToString());
                var userIdShuffled = GetChecksum(Salt2 + UserId);
                var userPasswordShuffled = GetChecksum((method == HttpMethod.Get ? poke : poke + data) + userIdShuffled + UserPasswordChecksum + randomLine);

                /*
                Logging.Debug("Poke argument base: " + Salt1 + UserId);
                Logging.Debug("Poke argument: " + GetChecksum(Salt1 + UserId));
                Logging.Debug("Poke: " + poke);
                Logging.Debug("Data: " + data);
                Logging.Debug("UserIdShuffled: " + userIdShuffled);
                Logging.Debug("UserPasswordChecksum: " + UserPasswordChecksum);
                Logging.Debug("RandomLine: " + randomLine);
                */

                request.Headers.Add("X-Validation", randomLine + userIdShuffled + userPasswordShuffled);
            }
            return request;
        }

        private static async Task<T> RunRequest<T>([NotNull] HttpRequestMessage request, [CanBeNull] Action<HttpStatusCode, HttpResponseHeaders> headersCallback,
                CancellationToken cancellation) {
            using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                await TestResponse(request.RequestUri.PathAndQuery, response, cancellation).ConfigureAwait(false);
                headersCallback?.Invoke(response.StatusCode, response.Headers);
                return typeof(T) == typeof(object)
                        ? (T)(object)null
                        : typeof(T) == typeof(byte[])
                                ? (T)(object)await LoadBinaryContent(response, cancellation)
                                : JsonConvert.DeserializeObject<T>(await LoadContent(response, cancellation));
            }
        }

        private static string CalculateUploadChecksum(byte[] data) {
            using (var sha1 = SHA1.Create()) {
                return ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(Salt5 + ToHexString(sha1.ComputeHash(data)))));
            }
        }

        internal async Task<TResult> RequestAsync<TData, TResult>(HttpMethod method, [Localizable(false), NotNull] string url,
                [Localizable(false), CanBeNull] TData data, Action<HttpStatusCode, HttpResponseHeaders> headersCallback = null,
                CancellationToken cancellation = default) {
            var serialized = data != null ? JsonConvert.SerializeObject(data) : null;
            var request = await CreateHttpRequestAsync(method, url, serialized, cancellation).ConfigureAwait(false);
            if (data != null) request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
            return await RunRequest<TResult>(request, headersCallback, cancellation).ConfigureAwait(false);
        }
        #endregion

        #region Various public methods for REST API
        public Task<TResult> GetAsync<TResult>([Localizable(false), NotNull] string url, CancellationToken cancellation = default) {
            return RequestAsync<object, TResult>(HttpMethod.Get, url, null, null, cancellation);
        }

        public Task<TResult> GetAsync<TResult>([Localizable(false), NotNull] string url, Action<HttpStatusCode, HttpResponseHeaders> headersCallback,
                CancellationToken cancellation = default) {
            return RequestAsync<object, TResult>(HttpMethod.Get, url, null, headersCallback, cancellation);
        }

        public Task PostAsync<TData>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] TData data,
                CancellationToken cancellation = default) {
            return PostAsync<TData, object>(url, data, cancellation);
        }

        public Task PostAsync([Localizable(false), NotNull] string url, CancellationToken cancellation = default) {
            return PostAsync<object, object>(url, null, cancellation);
        }

        public Task PatchAsync<TData>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] TData data,
                CancellationToken cancellation = default) {
            return PatchAsync<TData, object>(url, data, cancellation);
        }

        public Task PutAsync<TData>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] TData data,
                CancellationToken cancellation = default) {
            return PutAsync<TData, object>(url, data, cancellation);
        }

        public Task DeleteAsync<TData>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] TData data,
                CancellationToken cancellation = default) {
            return DeleteAsync<TData, object>(url, data, cancellation);
        }

        public Task<TResult> PostAsync<TData, TResult>([Localizable(false), NotNull] string url, [Localizable(false), CanBeNull] TData data,
                CancellationToken cancellation = default) {
            return RequestAsync<TData, TResult>(HttpMethod.Post, url, data, null, cancellation);
        }

        public Task<TResult> PatchAsync<TData, TResult>([Localizable(false), NotNull] string url, [Localizable(false), CanBeNull] TData data,
                CancellationToken cancellation = default) {
            return RequestAsync<TData, TResult>(new HttpMethod("PATCH"), url, data, null, cancellation);
        }

        public Task<TResult> PutAsync<TData, TResult>([Localizable(false), NotNull] string url, [Localizable(false), CanBeNull] TData data,
                CancellationToken cancellation = default) {
            return RequestAsync<TData, TResult>(HttpMethod.Put, url, data, null, cancellation);
        }

        public Task DeleteAsync([Localizable(false), NotNull] string url,
                CancellationToken cancellation = default) {
            return RequestAsync<object, object>(HttpMethod.Delete, url, null, null, cancellation);
        }

        public Task<TResult> DeleteAsync<TResult>([Localizable(false), NotNull] string url,
                CancellationToken cancellation = default) {
            return RequestAsync<object, TResult>(HttpMethod.Delete, url, null, null, cancellation);
        }

        public Task<TResult> DeleteAsync<TData, TResult>([Localizable(false), NotNull] string url, [Localizable(false), CanBeNull] TData data,
                CancellationToken cancellation = default) {
            return RequestAsync<TData, TResult>(HttpMethod.Delete, url, data, null, cancellation);
        }
        #endregion

        #region Simple file uploads
        public class UploadGroup {
            private readonly WorkshopClient _client;
            private readonly string _groupId;

            public UploadGroup(WorkshopClient client, string groupId) {
                _client = client;
                _groupId = groupId;
            }

            [ItemNotNull]
            public async Task<string> UploadAsync([NotNull] byte[] data, [NotNull] string fileName,
                    IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
                var checksum = CalculateUploadChecksum(data);
                var uploaderParams = await _client.PostAsync<object, JObject>($"/manage/files/{checksum}", new {
                    size = data.Length,
                    name = fileName,
                    group = _groupId
                }, cancellation).ConfigureAwait(false);

                var uploaderId = uploaderParams["uploaderID"]?.ToString();
                if (uploaderId == "reuse/1") {
                    return ((JObject)uploaderParams["params"])["downloadURL"].ToString();
                }

                var uploader = WorkshopUploaderFactory.Create(uploaderId, (JObject)uploaderParams["params"]);
                var result = await uploader.UploadAsync(data, _groupId, fileName, progress, cancellation).ConfigureAwait(false);
                return (await _client.PutAsync<object, JObject>($"/manage/files/{checksum}", new {
                    uploaderID = uploaderId,
                    size = result.Size,
                    tag = result.Tag
                }))["downloadURL"].ToString();
            }
        }

        public UploadGroup StartNewUploadGroup(string groupId = null) {
            return new UploadGroup(this,
                    groupId.Or(Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16).ToLowerInvariant()));
        }
        #endregion

        public async Task DownloadFileAsync(string sourceUrl, string destination, bool overwriteIfExists,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            if (File.Exists(destination) && !overwriteIfExists) return;

            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Starting to download…"));
            var downloadInfo = await PostAsync<JToken, WorkshopDownloadInformation>(sourceUrl, null, cancellation);
            cancellation.ThrowIfCancellationRequested();

            var temporaryFilenameProgress = $"{destination}.tmp";
            using (var client = new CookieAwareWebClient()) {
                var totalSize = -1L;
                var progressTimer = new AsyncProgressBytesStopwatch();
                await new DirectLoader(downloadInfo.DownloadUrl).DownloadAsync(client, (url, information) => {
                    totalSize = information.TotalSize ?? downloadInfo.DownloadSize;
                    return new FlexibleLoaderDestination(temporaryFilenameProgress, true);
                }, progress: new Progress<long>(x => { progress?.Report(AsyncProgressEntry.CreateDownloading(x, totalSize, progressTimer)); }),
                        cancellation: cancellation);
                cancellation.ThrowIfCancellationRequested();
            }
            File.Move(temporaryFilenameProgress, destination);
        }
    }
}