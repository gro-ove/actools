using System;
using System.Diagnostics;
using System.Net;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class CookieAwareWebClient : WebClient {
        private readonly CookieContainer _container = new CookieContainer();

        private string _method;

        public IDisposable SetMethod(string method) {
            var oldValue = _method;
            _method = method;
            return new ActionAsDisposable(() => {
                _method = oldValue;
            });
        }

        private string _contentType;

        public IDisposable SetContentType(string contentType) {
            var oldValue = _contentType;
            _contentType = contentType;
            return new ActionAsDisposable(() => {
                _contentType = oldValue;
            });
        }

        private bool? _autoRedirect;

        public IDisposable SetAutoRedirect(bool value) {
            var oldValue = _autoRedirect;
            _autoRedirect = value;
            return new ActionAsDisposable(() => {
                _autoRedirect = oldValue;
            });
        }

        public IDisposable SetProxy([CanBeNull] string proxy) {
            if (string.IsNullOrWhiteSpace(proxy)) return new ActionAsDisposable(() => { });

            var oldValue = Proxy;
            try {
                Proxy = string.IsNullOrWhiteSpace(proxy) ? null : new WebProxy(proxy);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t set proxy", e);
            }

            return new ActionAsDisposable(() => {
                Proxy = oldValue;
            });
        }

        public IDisposable SetUserAgent(string newUserAgent) {
            var oldValue = Headers[HttpRequestHeader.UserAgent];
            Headers[HttpRequestHeader.UserAgent] = newUserAgent;
            return new ActionAsDisposable(() => {
                Headers[HttpRequestHeader.UserAgent] = oldValue;
            });
        }

        protected override WebRequest GetWebRequest(Uri address) {
            var request = base.GetWebRequest(address);
            var webRequest = request as HttpWebRequest;

            if (webRequest != null) {
                webRequest.CookieContainer = _container;

                if (_autoRedirect.HasValue) {
                    webRequest.AllowAutoRedirect = _autoRedirect.Value;
                }

                if (!string.IsNullOrEmpty(_contentType)) {
                    webRequest.ContentType = _contentType;
                }

                if (!string.IsNullOrEmpty(_method)) {
                    webRequest.Method = _method;
                }
            }

            return request;
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

                progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, total));
            }

            DownloadProgressChanged += Handler;
            return new ActionAsDisposable(() => {
                DownloadProgressChanged -= Handler;
            });
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

                progress?.Report(AsyncProgressEntry.CreateUploading(args.BytesSent, total));
            }

            UploadProgressChanged += Handler;
            return new ActionAsDisposable(() => {
                UploadProgressChanged -= Handler;
            });
        }
    }
}