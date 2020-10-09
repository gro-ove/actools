using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Workshop.Uploaders;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop {
    public class WorkshopClient {
        private const string Salt1 = "Mghf4ZTzPQFGYJMA";
        private const string Salt2 = "5qzAfOyqBIgNHG0V";
        private const string Salt3 = "h3y2zIonOlgf1AQS";

        private string ApiHost;
        private string UserId;
        private string UserPassword;

        public WorkshopClient([NotNull] string apiHost, [NotNull] string userId, [NotNull] string userPassword) {
            ApiHost = apiHost;
            UserId = userId;
            UserPassword = GetChecksum(GetChecksum(Salt3 + userId) + userPassword);
        }

        [NotNull]
        private string GetFullUrl([NotNull] string path) {
            return ApiHost + path;
        }

        [NotNull]
        private string GetChecksum([NotNull] string s) {
            using (var sha1 = SHA256.Create()) {
                return sha1.ComputeHash(Encoding.UTF8.GetBytes(s)).ToHexString().ToLowerInvariant();
            }
        }

        [ItemNotNull]
        private async Task<string> PokeAsync([NotNull] string url, CancellationToken cancellation) {
            var request = new HttpRequestMessage(HttpMethod.Get, GetFullUrl("/poke/" + GetChecksum(Salt1 + url)));
            try {
                using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                    if (response.StatusCode != HttpStatusCode.OK) throw new WebException(response.ReasonPhrase);
                    return JObject.Parse(await response.Content.ReadAsStringAsync().WithCancellation(cancellation).ConfigureAwait(false))["poke"].ToString();
                }
            } catch (Exception e) when (e.IsCancelled()) {
                throw new WebException("Timeout exceeded", WebExceptionStatus.Timeout);
            }
        }

        [ItemNotNull]
        private async Task<HttpRequestMessage> CreateHttpRequestAsync(HttpMethod method, [NotNull] string url, [CanBeNull] string data,
                CancellationToken cancellation) {
            var poke = await PokeAsync(url, cancellation).ConfigureAwait(false);
            var randomLine = GetChecksum(Guid.NewGuid().ToString());
            var userIdShuffled = GetChecksum(Salt2 + UserId);
            var userPasswordShuffled = GetChecksum((method == HttpMethod.Get ? poke : poke + data) + userIdShuffled + UserPassword + randomLine);
            var request = new HttpRequestMessage(method, GetFullUrl(url));
            request.Headers.Add("X-Validation", randomLine + userIdShuffled + userPasswordShuffled);
            return request;
        }

        private static async Task<T> RunRequest<T>(HttpRequestMessage request, CancellationToken cancellation) {
            using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                if (response.StatusCode >= (HttpStatusCode)400) {
                    throw new WebException(
                            (await response.Content.ReadAsStringAsync().WithCancellation(cancellation).ConfigureAwait(false)).Or(response.ReasonPhrase));
                }
                return typeof(T) == typeof(object)
                        ? (T)(object)null
                        : JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync().WithCancellation(cancellation).ConfigureAwait(false));
            }
        }

        [CanBeNull]
        private JObject _uploaderParams;

        [CanBeNull]
        private IWorkshopUploader _uploader;

        public void MarkNewUploadGroup() {
            _uploader?.MarkNewGroup();
        }

        [ItemNotNull]
        public async Task<string> UploadAsync([NotNull] byte[] data, [NotNull] string downloadName, [CanBeNull] string origin = null,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            if (_uploader == null) {
                if (_uploaderParams == null) {
                    _uploaderParams = await GetAsync<JObject>("/manage/upload-params", cancellation).ConfigureAwait(false);
                    if (_uploaderParams == null) {
                        throw new Exception("Upload parameters are not available");
                    }
                }
                _uploader = WorkshopUploaderFactory.Create(_uploaderParams);
            }
            return await _uploader.UploadAsync(data, downloadName, origin, progress, cancellation).ConfigureAwait(false);
        }

        public async Task<T> GetAsync<T>([Localizable(false), NotNull] string url, CancellationToken cancellation = default) {
            var request = await CreateHttpRequestAsync(HttpMethod.Get, url, null, cancellation).ConfigureAwait(false);
            return await RunRequest<T>(request, cancellation).ConfigureAwait(false);
        }

        public async Task PostAsync<T>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] T data, CancellationToken cancellation = default) {
            // ReSharper disable once MethodHasAsyncOverload
            var serialized = JsonConvert.SerializeObject(data);
            var request = await CreateHttpRequestAsync(HttpMethod.Post, url, serialized, cancellation).ConfigureAwait(false);
            request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
            await RunRequest<object>(request, cancellation).ConfigureAwait(false);
        }

        public async Task PatchAsync<T>([Localizable(false), NotNull] string url, [Localizable(false), NotNull] T data, CancellationToken cancellation = default) {
            // ReSharper disable once MethodHasAsyncOverload
            var serialized = JsonConvert.SerializeObject(data);
            var request = await CreateHttpRequestAsync(new HttpMethod("PATCH"), url, serialized, cancellation).ConfigureAwait(false);
            request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
            await RunRequest<object>(request, cancellation).ConfigureAwait(false);
        }
    }
}