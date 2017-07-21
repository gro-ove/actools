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
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing {
    internal static class Request {
        internal static byte[] GetBytes(this string s) {
            return Encoding.UTF8.GetBytes(s);
        }

        internal static byte[] GetQuery(this NameValueCollection data) {
            return data.Keys.OfType<string>().Select(x => $"{HttpUtility.UrlEncode(x)}={HttpUtility.UrlEncode(data[x])}").JoinToString('&').GetBytes();
        }

        [ItemCanBeNull]
        private static async Task<string> Send([Localizable(false)] string method, string url, byte[] data, string authToken, CancellationToken cancellation) {
            try {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.UserAgent = InternalUtils.GetKunosUserAgent();

                if (authToken != null) {
                    request.Headers[@"Authorization"] = @"Bearer " + authToken;
                }

                if (data != null) {
                    request.ContentType = data[0] == '{' ? @"application/json" : @"application/x-www-form-urlencoded";
                    request.ContentLength = data.Length;

                    using (var stream = await request.GetRequestStreamAsync()) {
                        if (cancellation.IsCancellationRequested) return null;
                        await stream.WriteAsync(data, 0, data.Length, cancellation);
                        if (cancellation.IsCancellationRequested) return null;
                    }
                }

                string result;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream()) {
                    if (cancellation.IsCancellationRequested) return null;
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        result = await reader.ReadToEndAsync();
                    }
                }

                return result;
            } catch (WebException e) {
                using (var stream = e.Response.GetResponseStream()) {
                    Logging.Warning(e);
                    if (stream != null) {
                        Logging.Warning(new StreamReader(stream, Encoding.UTF8).ReadToEnd());
                    }
                }
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [ItemCanBeNull]
        private static async Task<T> Send<T>([Localizable(false)] string method, string url, byte[] data, string authToken, CancellationToken cancellation) {
            var response = await Send(method, url, data, authToken, cancellation);
            return response == null ? default(T) : JsonConvert.DeserializeObject<T>(response);
        }

        [ItemCanBeNull]
        public static Task<string> Get(string url, string authToken = null, CancellationToken cancellation = default(CancellationToken)) {
            return Send("GET", url, null, authToken, cancellation);
        }

        [ItemCanBeNull]
        public static Task<T> Get<T>(string url, string authToken = null, CancellationToken cancellation = default(CancellationToken)) {
            return Send<T>("GET", url, null, authToken, cancellation);
        }

        [ItemCanBeNull]
        public static Task<T> Post<T>(string url, byte[] data, string authToken = null, CancellationToken cancellation = default(CancellationToken)) {
            return Send<T>("POST", url, data, authToken, cancellation);
        }

        [ItemCanBeNull]
        public static Task<T> Put<T>(string url, byte[] data, string authToken = null, CancellationToken cancellation = default(CancellationToken)) {
            return Send<T>("PUT", url, data, authToken, cancellation);
        }

        [ItemCanBeNull]
        [Localizable(false)]
        public static async Task<T> PostMultipart<T>(string url, object metadata, string authToken, byte[] data, string contentType,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
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

                using (var stream = await request.GetRequestStreamAsync()) {
                    if (cancellation.IsCancellationRequested) return default(T);
                    progress?.Report(AsyncProgressEntry.CreateUploading(0, total));

                    await stream.WriteAsync(prefix, 0, prefix.Length, cancellation);
                    if (cancellation.IsCancellationRequested) return default(T);

                    const int blockSize = 10240;
                    for (var i = 0; i < data.Length; i += blockSize) {
                        progress?.Report(AsyncProgressEntry.CreateUploading(prefix.Length + i, total));
                        await stream.WriteAsync(data, i, Math.Min(blockSize, data.Length - i), cancellation);
                        if (cancellation.IsCancellationRequested) return default(T);
                    }

                    progress?.Report(AsyncProgressEntry.CreateUploading(prefix.Length + data.Length, total));

                    await stream.WriteAsync(postfix, 0, postfix.Length, cancellation);
                    if (cancellation.IsCancellationRequested) return default(T);
                }

                string result;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream()) {
                    if (cancellation.IsCancellationRequested) return default(T);
                    if (stream == null) return default(T);
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                        result = await reader.ReadToEndAsync();
                        if (cancellation.IsCancellationRequested) return default(T);
                    }
                }

                // Logging.Write("Upload result: " + result);
                return JsonConvert.DeserializeObject<T>(result);
            } catch (WebException e) {
                using (var stream = e.Response.GetResponseStream()) {
                    Logging.Warning(e);
                    if (stream != null) {
                        Logging.Warning(new StreamReader(stream, Encoding.UTF8).ReadToEnd());
                    }
                }
                return default(T);
            } catch (Exception e) {
                Logging.Warning(e);
                return default(T);
            }
        }
    }
}