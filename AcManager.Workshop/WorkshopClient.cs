using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
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
        internal string UserId {
            get => _userId;
            set {
                _userId = value;
                _pokeKey = null;
            }
        }

        [CanBeNull]
        private string _userPasswordChecksum;

        [CanBeNull]
        internal string UserPasswordChecksum {
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

        internal static string GetUserId([NotNull] string steamId) {
            if (steamId == null) throw new ArgumentNullException(nameof(steamId));
            using (var sha = SHA256.Create()) {
                return Regex.Replace(Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Salt4 + steamId))), @"\W+|=+$", "").Substring(0, 16);
            }
        }

        internal static string GetPasswordChecksum([NotNull] string userId, [NotNull] string userPassword) {
            if (userId == null) throw new ArgumentNullException(nameof(userId));
            if (userPassword == null) throw new ArgumentNullException(nameof(userPassword));
            Logging.Debug($"userId={userId}, userPassword=`{userPassword}`, checksum={GetChecksum(GetChecksum(Salt3 + userId) + userPassword)}");
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

        private static async Task TestResponse(HttpResponseMessage response, CancellationToken cancellation) {
            if (response.StatusCode >= (HttpStatusCode)400) {
                var errorMessage = response.ReasonPhrase;
                string remoteException = null;
                string[] remoteStackTrace = null;
                try {
                    var content = await LoadContent(response, cancellation);
                    var details = JObject.Parse(content);
                    errorMessage = details["error"].ToString();
                    var exception = details["exception"];
                    if (exception != null) {
                        remoteException = exception["message"].ToString();
                        remoteStackTrace = exception["stack"]?.ToObject<string[]>();
                        if (remoteStackTrace?.Length == 0) {
                            remoteStackTrace = null;
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                    // ignored
                }
                throw new WorkshopException(response.StatusCode, errorMessage, remoteException, remoteStackTrace);
            }
        }

        [ItemNotNull]
        private async Task<string> PokeAsync(CancellationToken cancellation) {
            var request = new HttpRequestMessage(HttpMethod.Post, GetFullUrl("/poke/" + GetChecksum(Salt1 + UserId)));
            try {
                using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                    await TestResponse(response, cancellation).ConfigureAwait(false);
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
                Logging.Debug("Poke argument base: " + Salt1 + UserId);
                Logging.Debug("Poke argument: " + GetChecksum(Salt1 + UserId));
                Logging.Debug("Poke: " + poke);
                Logging.Debug("Data: " + data);
                Logging.Debug("UserIdShuffled: " + userIdShuffled);
                Logging.Debug("UserPasswordChecksum: " + UserPasswordChecksum);
                Logging.Debug("RandomLine: " + randomLine);
                request.Headers.Add("X-Validation", randomLine + userIdShuffled + userPasswordShuffled);
            }
            return request;
        }

        private static async Task<T> RunRequest<T>([NotNull] HttpRequestMessage request, [CanBeNull] Action<HttpResponseHeaders> headersCallback,
                CancellationToken cancellation) {
            using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                await TestResponse(response, cancellation).ConfigureAwait(false);
                headersCallback?.Invoke(response.Headers);
                return typeof(T) == typeof(object)
                        ? (T)(object)null
                        : typeof(T) == typeof(byte[])
                                ? (T)(object)await LoadBinaryContent(response, cancellation)
                                : JsonConvert.DeserializeObject<T>(await LoadContent(response, cancellation));
            }
        }

        private string _currentGroup;

        public void MarkNewUploadGroup(string groupId = null) {
            _currentGroup = groupId.Or(Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16).ToLowerInvariant());
        }

        private static string CalculateUploadChecksum(byte[] data) {
            using (var sha1 = SHA1.Create()) {
                return ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(Salt5 + ToHexString(sha1.ComputeHash(data)))));
            }
        }

        internal async Task<TResult> RequestAsync<TData, TResult>(HttpMethod method, [Localizable(false), NotNull] string url,
                [Localizable(false), CanBeNull] TData data, Action<HttpResponseHeaders> headersCallback = null,
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

        public Task PostAsync<TData>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] TData data,
                CancellationToken cancellation = default) {
            return PostAsync<TData, object>(url, data, cancellation);
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

        public Task<TResult> DeleteAsync<TData, TResult>([Localizable(false), NotNull] string url, [Localizable(false), CanBeNull] TData data,
                CancellationToken cancellation = default) {
            return RequestAsync<TData, TResult>(HttpMethod.Delete, url, data, null, cancellation);
        }
        #endregion

        #region Simple file uploads
        [ItemNotNull]
        public async Task<string> UploadAsync([NotNull] byte[] data, [NotNull] string fileName,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            var checksum = CalculateUploadChecksum(data);
            var uploaderParams = await PostAsync<object, JObject>("/manage/urls", new {
                size = data.Length,
                name = fileName,
                group = _currentGroup,
                checksum
            }, cancellation).ConfigureAwait(false);

            var uploaderId = uploaderParams["uploaderID"].ToString();
            if (uploaderId == "reuse/1") {
                return ((JObject)uploaderParams["params"])["downloadURL"].ToString();
            }

            var uploader = WorkshopUploaderFactory.Create(uploaderId, (JObject)uploaderParams["params"]);
            var result = await uploader.UploadAsync(data, _currentGroup, fileName, progress, cancellation).ConfigureAwait(false);
            return (await PutAsync<object, JObject>("/manage/urls", new {
                checksum,
                uploaderID = uploaderId,
                size = result.Size,
                tag = result.Tag
            }))["downloadURL"].ToString();
        }
        #endregion

        #region Authorization process
        #endregion
    }
}