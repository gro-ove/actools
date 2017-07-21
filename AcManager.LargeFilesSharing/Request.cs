using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing {
    internal static class Request {
        private static byte[] GetBytes(this string s) {
            return Encoding.UTF8.GetBytes(s);
        }

        private static byte[] GetQuery(this NameValueCollection data) {
            return data.Keys.OfType<string>().Where(x => data[x] != null)
                       .Select(x => $"{HttpUtility.UrlEncode(x)}={HttpUtility.UrlEncode(data[x])}").JoinToString('&').GetBytes();
        }

        [NotNull]
        private static byte[] GetBytes([NotNull] object data, out string contentType) {
            switch (data) {
                case NameValueCollection nv:
                    contentType = @"application/x-www-form-urlencoded";
                    return nv.GetQuery();
                case byte[] bytes:
                    contentType = @"application/octet-stream";
                    return bytes;
                default:
                    contentType = @"application/json";
                    return JsonConvert.SerializeObject(data).GetBytes();
            }
        }

        [ItemCanBeNull]
        private static Task<string> Send([Localizable(false)] string method, string url, object data, string authToken,
                [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation, NameValueCollection extraHeaders) {
            try {
                using (var order = KillerOrder.Create(new CookieAwareWebClient {
                    Headers = { [@"Authorization"] = @"Bearer " + authToken }
                }, TimeSpan.FromMinutes(10))) {
                    var client = order.Victim;
                    client.SetUserAgent(InternalUtils.GetKunosUserAgent());
                    cancellation.Register(client.CancelAsync);

                    foreach (string header in extraHeaders) {
                        client.Headers[header] = extraHeaders[header];
                    }

                    if (data == null) {
                        client.SetMethod(method);
                        client.SetDownloadProgress(progress, null, order.Delay);
                        return Task.Run(() => client.DownloadData(url))
                                   .ContinueWith(x => x.Result.ToUtf8String(), TaskContinuationOptions.OnlyOnRanToCompletion);
                    } else {
                        var bytes = GetBytes(data, out var contentType);
                        client.SetContentType(contentType);
                        client.SetUploadProgress(progress, bytes.Length, order.Delay);
                        return Task.Run(() => client.UploadData(url, method, bytes))
                                   .ContinueWith(x => x.Result.ToUtf8String(), TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                }
            } catch (Exception) when (cancellation.IsCancellationRequested) {
                return null;
            } catch (WebException e) {
                Logging.Warning(e);
                using (var stream = e.Response?.GetResponseStream()) {
                    if (stream != null) {
                        Logging.Warning(new StreamReader(stream, Encoding.UTF8).ReadToEnd());
                    }
                }
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }



            /*try {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.SendChunked = true;
                request.AllowWriteStreamBuffering = false;

                request.Method = method;
                request.UserAgent = InternalUtils.GetKunosUserAgent();

                if (authToken != null) {
                    request.Headers[@"Authorization"] = @"Bearer " + authToken;
                }

                if (extraHeaders != null) {
                    foreach (string header in extraHeaders) {
                        request.Headers[header] = extraHeaders[header];
                    }
                }

                if (data != null) {
                    request.ContentType = GetContentType(data, index, count);
                    using (var stream = await request.GetRequestStreamAsync()) {
                        if (cancellation.IsCancellationRequested) return null;

                        var total = data.Length;
                        const int blockSize = 51200;
                        for (var i = 0; i < count; i += blockSize) {
                            progress?.Report(AsyncProgressEntry.CreateUploading(i, total));
                            await stream.WriteAsync(data, i + index, Math.Min(blockSize, count - i), cancellation);
                            if (cancellation.IsCancellationRequested) return null;
                        }
                    }

                    Logging.Here();
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
                Logging.Warning(e);
                using (var stream = e.Response?.GetResponseStream()) {
                    if (stream != null) {
                        Logging.Warning(new StreamReader(stream, Encoding.UTF8).ReadToEnd());
                    }
                }
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }*/
        }

        [ItemCanBeNull]
        private static Task<T> Send<T>([Localizable(false)] string method, string url, [CanBeNull] object data, string authToken,
                NameValueCollection extraHeaders, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            return Send(method, url, data, authToken, progress, cancellation, extraHeaders)
                    .ContinueWith(x => x.Result == null ? default(T) : JsonConvert.DeserializeObject<T>(x.Result));
        }

        [ItemCanBeNull]
        public static Task<string> Get(string url, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send("GET", url, null, authToken, progress, cancellation, extraHeaders);
        }

        [ItemCanBeNull]
        public static Task<T> Get<T>(string url, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send<T>("GET", url, null, authToken, extraHeaders, progress, cancellation);
        }

        public static Task<string> Post(string url, [CanBeNull] object data, [CanBeNull] string authToken, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken), NameValueCollection extraHeaders = null) {
            return Send("POST", url, data, authToken, progress, cancellation, extraHeaders);
        }

        [ItemCanBeNull]
        public static Task<T> Post<T>(string url, [CanBeNull] object data, [CanBeNull] string authToken,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken),
                NameValueCollection extraHeaders = null) {
            return Send<T>("POST", url, data, authToken, extraHeaders, progress, cancellation);
        }

        [ItemCanBeNull, Localizable(false)]
        public static async Task<string> PostMultipart(string url, object metadata, string authToken, [NotNull] byte[] data, string contentType,
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

                using (var stream = await request.GetRequestStreamAsync()) {
                    if (cancellation.IsCancellationRequested) return null;
                    progress?.Report(AsyncProgressEntry.CreateUploading(0, total));

                    await stream.WriteAsync(prefix, 0, prefix.Length, cancellation);
                    if (cancellation.IsCancellationRequested) return null;

                    const int blockSize = 10240;
                    for (var i = 0; i < data.Length; i += blockSize) {
                        progress?.Report(AsyncProgressEntry.CreateUploading(prefix.Length + i, total));
                        await stream.WriteAsync(data, i, Math.Min(blockSize, data.Length - i), cancellation);
                        if (cancellation.IsCancellationRequested) return null;
                    }

                    progress?.Report(AsyncProgressEntry.CreateUploading(prefix.Length + data.Length, total));

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
            } catch (WebException e) {
                if (cancellation.IsCancellationRequested) {
                    return null;
                }

                Logging.Warning(e);
                using (var stream = e.Response?.GetResponseStream()) {
                    if (stream != null) {
                        Logging.Warning(new StreamReader(stream, Encoding.UTF8).ReadToEnd());
                    }
                }

                return null;
            } catch (Exception e) {
                if (cancellation.IsCancellationRequested) {
                    return null;
                }

                Logging.Warning(e);
                return null;
            }
        }

        [ItemCanBeNull, Localizable(false)]
        public static Task<T> PostMultipart<T>(string url, object metadata, string authToken, [NotNull] byte[] data, string contentType,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken),
                NameValueCollection extraHeaders = null) {
            return PostMultipart(url, metadata, authToken, data, contentType, progress, cancellation, extraHeaders)
                    .ContinueWith(x => x.Result == null ? default(T) : JsonConvert.DeserializeObject<T>(x.Result));
        }
    }
}