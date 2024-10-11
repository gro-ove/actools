using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public class CookieAwareWebClient : WebClient {
        public CookieAwareWebClient() {
            Headers[HttpRequestHeader.UserAgent] = CmApiProvider.CommonUserAgent;
            Encoding = Encoding.UTF8;
        }

        private class CookieContainer {
            private class CookieHolder {
                private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

                public string this[string key] {
                    get => _values.TryGetValue(key, out var cookie) ? cookie : null;
                    set {
                        _values[key] = value;
                        _value.Reset();
                    }
                }

                public CookieHolder() {
                    _value = Lazier.Create(() => _values.Select(x => $@"{x.Key}={x.Value}").JoinToString(';'));
                }

                private readonly Lazier<string> _value;
                public string Value => _value.Value;

            }

            private readonly Dictionary<string, CookieHolder> _cookies;

            private string GetKey(Uri url) {
                return Regex.Match(url.Host, @"[^.]+\.[^.]+$").Value.Or(url.Host);
            }

            public string this[Uri url] {
                get => _cookies.TryGetValue(GetKey(url), out var cookie) ? cookie.Value : null;
                set {
                    var s = value.Split(new[] { '=' }, 2);
                    if (s.Length != 2) return;

                    var v = s[1].Split(new[] { ';' }, 2)[0];
                    if (!_cookies.TryGetValue(GetKey(url), out var cookie)) {
                        cookie = _cookies[GetKey(url)] = new CookieHolder();
                    }

                    cookie[s[0]] = v;
                }
            }

            public CookieContainer() {
                _cookies = new Dictionary<string, CookieHolder>();
            }
        }

        public IDisposable MaskAsCommonBrowser() {
            return SetUserAgent(CmApiProvider.CommonUserAgent);
        }

        private readonly CookieContainer _container = new CookieContainer();

        public HttpStatusCode StatusCode {
            get {
                if (_lastRequest != null && base.GetWebResponse(_lastRequest) is HttpWebResponse response) {
                    return response.StatusCode;
                }

                throw new InvalidOperationException(@"No request were made to get status code");
            }
        }

        private string _method;

        public IDisposable SetMethod(string method) {
            var oldValue = _method;
            _method = method;
            return new ActionAsDisposable(() => _method = oldValue);
        }

        private string _contentType;

        public IDisposable SetContentType(string contentType) {
            var oldValue = _contentType;
            _contentType = contentType;
            return new ActionAsDisposable(() => _contentType = oldValue);
        }

        private string _accept;

        public IDisposable SetAccept(string accept) {
            var oldValue = _accept;
            _accept = accept;
            return new ActionAsDisposable(() => _accept = oldValue);
        }

        private bool? _autoRedirect;

        public IDisposable SetAutoRedirect(bool value) {
            var oldValue = _autoRedirect;
            _autoRedirect = value;
            return new ActionAsDisposable(() => _autoRedirect = oldValue);
        }

        private Tuple<long, long> _range;

        [CanBeNull]
        public Tuple<long, long> CurrentRangeToLoad => _range;

        public IDisposable SetRange(Tuple<long, long> value) {
            var oldValue = _range;
            _range = value;
            return new ActionAsDisposable(() => _range = oldValue);
        }

        private bool _debugMode;

        public IDisposable SetDebugMode(bool value) {
            var oldValue = _debugMode;
            _debugMode = value;
            return new ActionAsDisposable(() => _debugMode = oldValue);
        }

        public IDisposable SetProxy([CanBeNull] string proxy) {
            if (string.IsNullOrWhiteSpace(proxy)) return new ActionAsDisposable(() => { });

            var oldValue = Proxy;
            try {
                Proxy = string.IsNullOrWhiteSpace(proxy) ? null : new WebProxy(proxy);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t set proxy", e);
            }

            return new ActionAsDisposable(() => Proxy = oldValue);
        }

        [Localizable(false)]
        public IDisposable SetUserAgent(string newUserAgent) {
            var oldValue = Headers[HttpRequestHeader.UserAgent];
            Headers[HttpRequestHeader.UserAgent] = newUserAgent;
            return new ActionAsDisposable(() => Headers[HttpRequestHeader.UserAgent] = oldValue);
        }

        [CanBeNull]
        public new WebHeaderCollection ResponseHeaders => base.ResponseHeaders;

        public void LogResponseHeaders([CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Logging.Debug(ResponseHeaders?.AllKeys.Select(x => $"{x}: {ResponseHeaders[x]}").JoinToString('\n') ?? "No response headers set", m, p, l);
        }

        public async Task<long?> GetContentSize(string url) {
            using (SetMethod("HEAD"))
            using (SetAutoRedirect(false)) {
                await DownloadStringTaskAsync(url);
                return ResponseHeaders != null && long.TryParse(ResponseHeaders[HttpResponseHeader.ContentLength],
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var length) ? length : (long?)null;
            }
        }

        private WebRequest _lastRequest;

        protected override WebRequest GetWebRequest(Uri address) {
            if (_debugMode) Logging.Debug(address);

            var request = base.GetWebRequest(address);
            if (request is HttpWebRequest webRequest) {
                var cookie = _container[address];
                if (cookie != null) {
                    webRequest.Headers.Set("Cookie", cookie);
                }

                if (_debugMode) {
                    Logging.Debug("Cookies:\n" + cookie);
                }

                if (_autoRedirect.HasValue) {
                    webRequest.AllowAutoRedirect = _autoRedirect.Value;
                }

                if (!string.IsNullOrEmpty(_contentType)) {
                    webRequest.ContentType = _contentType;
                }

                if (!string.IsNullOrEmpty(_accept)) {
                    webRequest.Accept = _accept;
                }

                if (!string.IsNullOrEmpty(_method)) {
                    webRequest.Method = _method;
                }

                if (_range != null && _range.Item1 >= 0) {
                    if (_range.Item2 < 0) {
                        webRequest.AddRange(_range.Item1);
                    } else {
                        webRequest.AddRange(_range.Item1, _range.Item2);
                    }
                }

                if (_debugMode) {
                    Logging.Debug("AllowAutoRedirect: " + webRequest.AllowAutoRedirect + "; ContentType: " + webRequest.ContentType + "; Accept: " +
                            webRequest.Accept + "; Method: " + webRequest.Method);
                }
            }

            _lastRequest = request;
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result) {
            var response = base.GetWebResponse(request, result);

            if (_debugMode) {
                try {
                    Logging.Debug("Response: " + (int?)(response as HttpWebResponse)?.StatusCode);
                    Logging.Debug("Headers:\n" + response?.Headers?.OfType<string>().Select(x => $"{x}: {response.Headers.Get(x)}").JoinToString('\n'));
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            ResponseUri = response?.ResponseUri;
            ResponseLocation = response?.Headers?.GetValues("Location")?.FirstOrDefault();

            var cookies = response?.Headers?.GetValues("Set-Cookie");
            if (cookies != null) {
                foreach (var c in cookies) {
                    _container[response.ResponseUri] = c;
                }
            }

            return response;
        }

        [CanBeNull]
        public Uri ResponseUri { get; private set; }

        public string ResponseLocation { get; private set; }

        protected override WebResponse GetWebResponse(WebRequest request) {
            var response = base.GetWebResponse(request);

            if (_debugMode) {
                try {
                    Logging.Debug("Response: " + (int?)(response as HttpWebResponse)?.StatusCode);
                    Logging.Debug("Headers:\n" + response?.Headers?.OfType<string>().Select(x => $"{x}: {response.Headers.Get(x)}").JoinToString('\n'));
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            ResponseUri = response?.ResponseUri;
            ResponseLocation = response?.Headers?.GetValues("Location")?.FirstOrDefault();

            var cookies = response?.Headers?.GetValues("Set-Cookie"); 
            if (cookies != null) {
                foreach (var c in cookies) {
                    _container[response.ResponseUri] = c;
                }
            }

            return response;
        }

        public IDisposable SetDownloadProgress([CanBeNull] IProgress<AsyncProgressEntry> progress, long? suggestedTotal = null, Action callback = null,
                AsyncProgressBytesStopwatch stopwatch = null) {
            var s = Stopwatch.StartNew();

            void Handler(object sender, DownloadProgressChangedEventArgs args) {
                if (s.Elapsed.TotalMilliseconds < 20) return;

                callback?.Invoke();
                s.Restart();

                var total = args.TotalBytesToReceive;
                if (total == -1 && suggestedTotal != null && suggestedTotal > 0) {
                    total = Math.Max(suggestedTotal.Value, args.BytesReceived);
                }

                progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, total, stopwatch));
            }

            DownloadProgressChanged += Handler;
            return new ActionAsDisposable(() => DownloadProgressChanged -= Handler);
        }

        public IDisposable SetUploadProgress([CanBeNull] IProgress<AsyncProgressEntry> progress, long? suggestedTotal = null, Action callback = null,
                AsyncProgressBytesStopwatch stopwatch = null) {
            var s = Stopwatch.StartNew();

            void Handler(object sender, UploadProgressChangedEventArgs args) {
                if (s.Elapsed.TotalMilliseconds < 20) return;

                callback?.Invoke();
                s.Restart();

                var total = args.TotalBytesToSend;
                if (total == -1 && suggestedTotal != null && suggestedTotal > 0) {
                    total = Math.Max(suggestedTotal.Value, args.BytesSent);
                }

                progress?.Report(AsyncProgressEntry.CreateUploading(args.BytesSent, total, stopwatch));
            }

            UploadProgressChanged += Handler;
            return new ActionAsDisposable(() => UploadProgressChanged -= Handler);
        }

        private static bool TryDecode5987(string input, out string output) {
            output = null;

            var quoteIndex = input.IndexOf('\'');
            if (quoteIndex == -1) {
                return false;
            }

            var lastQuoteIndex = input.LastIndexOf('\'');
            if (quoteIndex == lastQuoteIndex || input.IndexOf('\'', quoteIndex + 1) != lastQuoteIndex) {
                return false;
            }

            var encodingString = input.Substring(0, quoteIndex);
            var dataString = input.Substring(lastQuoteIndex + 1, input.Length - (lastQuoteIndex + 1));

            var decoded = new StringBuilder();
            try {
                var encoding = Encoding.GetEncoding(encodingString);
                var unescapedBytes = new byte[dataString.Length];
                var unescapedBytesCount = 0;
                for (var index = 0; index < dataString.Length; index++) {
                    if (Uri.IsHexEncoding(dataString, index)) {
                        unescapedBytes[unescapedBytesCount++] = (byte)Uri.HexUnescape(dataString, ref index);
                        index--;
                    } else {
                        if (unescapedBytesCount > 0) {
                            decoded.Append(encoding.GetString(unescapedBytes, 0, unescapedBytesCount));
                            unescapedBytesCount = 0;
                        }
                        decoded.Append(dataString[index]);
                    }
                }

                if (unescapedBytesCount > 0) {
                    decoded.Append(encoding.GetString(unescapedBytes, 0, unescapedBytesCount));
                }
            } catch (ArgumentException) {
                return false;
            }

            output = decoded.ToString();
            return true;
        }

        public bool TryGetFileName(out string filename) {
            try {
                var headers = ResponseHeaders;
                if (headers == null) {
                    filename = null;
                    return false;
                }
                var contentDisposition = headers["Content-Disposition"]?.Split(';').Select(x => x.Split(new[] { '=' }, 2)).Where(x => x.Length == 2)
                        .ToDictionary(x => x[0].Trim().ToLowerInvariant(), x => x[1].Trim());
                if (contentDisposition != null) {
                    if (contentDisposition.TryGetValue("filename", out var value)) {
                        filename = JsonConvert.DeserializeObject<string>(value);
                        return true;
                    }

                    if (contentDisposition.TryGetValue("filename*", out var filenameStar)) {
                        filename = TryDecode5987(filenameStar, out var decoded) ? decoded : null;
                        return true;
                    }
                }

                var bzFileName = headers[@"x-bz-file-name"];
                if (bzFileName != null) {
                    var match = Regex.Match(bzFileName, @"^c/[^\/]+/([^\/]+)\.\w+$");
                    filename = (match.Success && match.Groups.Count == 2 ? match.Groups[1].Value : null).Or(Path.GetFileName(bzFileName));
                    return true;
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            filename = null;
            return false;
        }
        
        public async Task<string> GetFinalRedirectAsync(string url, int maxRedirectCount = 8, Action<string> fileNameCallback = null) {
            using (SetAutoRedirect(false))
            using (SetMethod(@"GET")) {
                var newUrl = url;
                do {
                    try {
                        (await OpenReadTaskAsync(newUrl).ConfigureAwait(false)).Dispose();
                        // await DownloadStringTaskAsync(newUrl).ConfigureAwait(false);
                        switch (StatusCode) {
                            case HttpStatusCode.OK:
                                return newUrl;
                            case HttpStatusCode.Redirect:
                            case HttpStatusCode.MovedPermanently:
                            case HttpStatusCode.RedirectKeepVerb:
                            case HttpStatusCode.RedirectMethod:
                                if (ResponseHeaders == null) return newUrl;
                                newUrl = ResponseHeaders[@"Location"];
                                if (newUrl == null) return url;
                                if (newUrl.IndexOf(@"://", StringComparison.Ordinal) == -1) {
                                    // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                                    var u = new Uri(new Uri(url), newUrl);
                                    newUrl = u.ToString();

                                    if (fileNameCallback != null) {
                                        TryGetFileName(out var fileName);
                                        fileNameCallback(fileName);
                                    }
                                }
                                break;
                            default:
                                return newUrl;
                        }
                    } catch (WebException) {
                        return newUrl;
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                        return null;
                    }
                } while (--maxRedirectCount > 0);
                return newUrl;
            }
        }
    }
}