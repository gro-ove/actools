using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.LargeFilesSharing {
    public class ApiException : Exception {
        public ApiException(HttpStatusCode status, string statusDescription, string response, Exception e)
                : base(response == null ? $"{statusDescription} ({(int)status})" : $"{statusDescription} ({(int)status}): " + response, e) {
            Status = status;
            Response = response;
        }

        public HttpStatusCode Status { get; }
        public string Response { get; }

        [CanBeNull]
        public static Exception Wrap(Exception e, CancellationToken cancellation = default(CancellationToken)) {
            cancellation.ThrowIfCancellationRequested();

            var w = e as WebException;
            var r = w?.Response as HttpWebResponse;
            if (r != null) {
                string response = null;
                using (var stream = r.GetResponseStream()) {
                    if (stream != null) {
                        response = new StreamReader(stream, Encoding.UTF8).ReadToEnd();

                        try {
                            var jObj = JObject.Parse(response);
                            response = jObj.ToString(Formatting.Indented);
                        } catch (Exception) {
                            // ignored
                        }
                    }
                }

                var message = HttpWorkerRequest.GetStatusDescription((int)r.StatusCode);
                if (string.IsNullOrWhiteSpace(message)) {
                    message = r.StatusDescription;
                }

                return new ApiException(r.StatusCode, message, response, w);
            }

            return null;
        }
    }

    internal class Request {
        public string AuthorizationTokenType { get; set; } = "Bearer";

        private byte[] GetBytes(string s) {
            return Encoding.UTF8.GetBytes(s);
        }

        private byte[] GetQuery(NameValueCollection data) {
            return GetBytes(data.Keys.OfType<string>().Where(x => data[x] != null)
                                .Select(x => $"{HttpUtility.UrlEncode(x)}={HttpUtility.UrlEncode(data[x])}").JoinToString('&'));
        }

        [NotNull]
        private byte[] GetBytes([NotNull] object data, out string contentType) {
            switch (data) {
                case NameValueCollection nv:
                    contentType = @"application/x-www-form-urlencoded";
                    return GetQuery(nv);
                case byte[] bytes:
                    contentType = @"application/octet-stream";
                    return bytes;
                default:
                    contentType = @"application/json";
                    return GetBytes(JsonConvert.SerializeObject(data));
            }
        }

        [ItemCanBeNull]
        public async Task<string> Send([Localizable(false)] string method, string url, object data, string authToken,
                [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation, NameValueCollection extraHeaders) {
            try {
                using (var order = KillerOrder.Create(new CookieAwareWebClient {
                    Headers = { [@"Accept"] = @"application/json" }
                }, TimeSpan.FromMinutes(10))) {
                    var client = order.Victim;

                    if (authToken != null) {
                        client.Headers[@"Authorization"] = $@"{AuthorizationTokenType} {authToken}";
                    }

                    client.SetUserAgent(InternalUtils.GetKunosUserAgent());
                    cancellation.Register(client.CancelAsync);

                    if (extraHeaders != null) {
                        foreach (string header in extraHeaders) {
                            client.Headers[header] = extraHeaders[header];
#if DEBUG
                            Logging.Debug($"Header: {header}: {extraHeaders[header]}");
#endif
                        }
                    }

                    if (data == null) {
                        client.SetMethod(method);
                        client.SetDownloadProgress(progress, null, order.Delay);
                        return (await client.DownloadDataTaskAsync(url)).ToUtf8String();
                    }

                    var bytes = GetBytes(data, out var contentType);
                    client.SetContentType(contentType);
                    client.SetUploadProgress(progress, bytes.Length, order.Delay);
                    return (await client.UploadDataTaskAsync(url, method, bytes)).ToUtf8String();
                }
            } catch (Exception e) {
                var wrapped = ApiException.Wrap(e, cancellation);
                if (wrapped == null) throw;
                throw wrapped;
            }
        }

        [ItemCanBeNull]
        public async Task<T> Send<T>([Localizable(false)] string method, string url, [CanBeNull] object data, string authToken,
                NameValueCollection extraHeaders, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var result = await Send(method, url, data, authToken, progress, cancellation, extraHeaders);
#if DEBUG
            Logging.Debug(url);
            Logging.Debug(result);
#endif
            return result == null ? default(T) : JsonConvert.DeserializeObject<T>(result);
        }

        [ItemCanBeNull]
        public Task<string> Get(string url, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send("GET", url, null, authToken, progress, cancellation, extraHeaders);
        }

        [ItemCanBeNull]
        public Task<T> Get<T>(string url, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send<T>("GET", url, null, authToken, extraHeaders, progress, cancellation);
        }

        public Task<string> Post(string url, [CanBeNull] object data, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send("POST", url, data, authToken, progress, cancellation, extraHeaders);
        }

        [ItemCanBeNull]
        public Task<T> Post<T>(string url, [CanBeNull] object data, [CanBeNull] string authToken,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken),
                NameValueCollection extraHeaders = null) {
            return Send<T>("POST", url, data, authToken, extraHeaders, progress, cancellation);
        }

        public Task<string> Put(string url, [CanBeNull] object data, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send("PUT", url, data, authToken, progress, cancellation, extraHeaders);
        }

        [ItemCanBeNull]
        public Task<T> Put<T>(string url, [CanBeNull] object data, [CanBeNull] string authToken,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken),
                NameValueCollection extraHeaders = null) {
            return Send<T>("PUT", url, data, authToken, extraHeaders, progress, cancellation);
        }

        [ItemCanBeNull, Localizable(false)]
        public async Task<string> PostMultipart(string url, object metadata, string authToken, [NotNull] byte[] data, string contentType,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken),
                NameValueCollection extraHeaders = null) {
            try {
                const string boundary = "--fdfmkj4ixeyfzuxr6q3yp66ry53lerk98g33ow29e0khjjor";

                var prefix = Encoding.UTF8.GetBytes(boundary + "\nContent-Type: application/json; charset=UTF-8\n\n" +
                        JsonConvert.SerializeObject(metadata) + "\n\n" + boundary + "\nContent-Type: " + contentType + "\n\n");
                var postfix = Encoding.UTF8.GetBytes("\n" + boundary + "--");
                var total = prefix.Length + data.Length + postfix.Length;

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.UserAgent = InternalUtils.GetKunosUserAgent();
                request.ContentType = "multipart/related; boundary=" + boundary.Substring(2);
                request.ContentLength = total;
                request.Headers["Authorization"] = "Bearer " + authToken;

                if (extraHeaders != null) {
                    foreach (string header in extraHeaders) {
                        request.Headers[header] = extraHeaders[header];
                    }
                }

                var stopwatch = new AsyncProgressBytesStopwatch();
                using (var stream = await request.GetRequestStreamAsync()) {
                    if (cancellation.IsCancellationRequested) return null;
                    progress?.Report(AsyncProgressEntry.CreateUploading(0, total, stopwatch));

                    await stream.WriteAsync(prefix, 0, prefix.Length, cancellation);
                    if (cancellation.IsCancellationRequested) return null;

                    const int blockSize = 10240;
                    for (var i = 0; i < data.Length; i += blockSize) {
                        progress?.Report(AsyncProgressEntry.CreateUploading(prefix.Length + i, total, stopwatch));
                        await stream.WriteAsync(data, i, Math.Min(blockSize, data.Length - i), cancellation);
                        if (cancellation.IsCancellationRequested) return null;
                    }

                    progress?.Report(AsyncProgressEntry.CreateUploading(prefix.Length + data.Length, total, stopwatch));

                    await stream.WriteAsync(postfix, 0, postfix.Length, cancellation);
                    if (cancellation.IsCancellationRequested) return null;
                }

                string result;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream()) {
                    if (cancellation.IsCancellationRequested) return null;
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        result = await reader.ReadToEndAsync();
                        if (cancellation.IsCancellationRequested) return null;
                    }
                }

                return result;
            } catch (Exception e) {
                var wrapped = ApiException.Wrap(e, cancellation);
                if (wrapped == null) throw;
                throw wrapped;
            }
        }

        [ItemCanBeNull, Localizable(false)]
        public Task<T> PostMultipart<T>(string url, object metadata, string authToken, [NotNull] byte[] data, string contentType,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken),
                NameValueCollection extraHeaders = null) {
            return PostMultipart(url, metadata, authToken, data, contentType, progress, cancellation, extraHeaders)
                    .ContinueWith(x => x.Result == null ? default(T) : JsonConvert.DeserializeObject<T>(x.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}