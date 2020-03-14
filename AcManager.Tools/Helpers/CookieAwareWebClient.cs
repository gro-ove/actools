using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
                    ((HttpWebRequest)request).Headers.Set("Cookie", cookie);
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
                        ((HttpWebRequest)request).AddRange(_range.Item1);
                    } else {
                        ((HttpWebRequest)request).AddRange(_range.Item1, _range.Item2);
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

        public IDisposable SetDownloadProgress([CanBeNull] IProgress<AsyncProgressEntry> progress, long? suggestedTotal = null, Action callback = null) {
            var s = Stopwatch.StartNew();

            void Handler(object sender, DownloadProgressChangedEventArgs args) {
                if (s.Elapsed.TotalMilliseconds < 20) return;

                callback?.Invoke();
                s.Restart();

                var total = args.TotalBytesToReceive;
                if (total == -1 && suggestedTotal != null && suggestedTotal > 0) {
                    total = Math.Max(suggestedTotal.Value, args.BytesReceived);
                }

                progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, total, null));
            }

            DownloadProgressChanged += Handler;
            return new ActionAsDisposable(() => DownloadProgressChanged -= Handler);
        }

        public IDisposable SetUploadProgress([CanBeNull] IProgress<AsyncProgressEntry> progress, long? suggestedTotal = null, Action callback = null) {
            var s = Stopwatch.StartNew();

            void Handler(object sender, UploadProgressChangedEventArgs args) {
                if (s.Elapsed.TotalMilliseconds < 20) return;

                callback?.Invoke();
                s.Restart();

                var total = args.TotalBytesToSend;
                if (total == -1 && suggestedTotal != null && suggestedTotal > 0) {
                    total = Math.Max(suggestedTotal.Value, args.BytesSent);
                }

                progress?.Report(AsyncProgressEntry.CreateUploading(args.BytesSent, total, null));
            }

            UploadProgressChanged += Handler;
            return new ActionAsDisposable(() => UploadProgressChanged -= Handler);
        }

        public async Task<string> GetFinalRedirectAsync(string url, int maxRedirectCount = 8) {
            using (SetAutoRedirect(false))
            using (SetMethod(@"HEAD")) {
                var newUrl = url;
                do {
                    try {
                        await DownloadStringTaskAsync(newUrl).ConfigureAwait(false);
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