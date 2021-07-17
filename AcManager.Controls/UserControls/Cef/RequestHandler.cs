using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.UserControls.Web;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Cef {
    internal class RequestHandler : IRequestHandler, IResourceRequestHandler, ICookieAccessFilter {
        [CanBeNull]
        internal string UserAgent { get; set; }

        [CanBeNull]
        internal ICustomStyleProvider StyleProvider { get; set; }

        bool IRequestHandler.OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect) {
            // if (request.TransitionType.HasFlag(TransitionType.ForwardBack)) return true;
            return RequestsFiltering.ShouldBeBlocked(request.Url);
        }

        void IRequestHandler.OnDocumentAvailableInMainFrame(IWebBrowser chromiumWebBrowser, IBrowser browser) { }

        bool IRequestHandler.OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl,
                WindowOpenDisposition targetDisposition,
                bool userGesture) {
            return false;
        }

        IResourceRequestHandler IRequestHandler.GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request,
                bool isNavigation,
                bool isDownload, string requestInitiator, ref bool disableDefaultHandling) {
            return this;
        }

        bool IRequestHandler.GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port,
                string realm, string scheme,
                IAuthCallback callback) {
            return true;
        }

        bool IRequestHandler.OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo,
                IRequestCallback callback) {
            if (!callback.IsDisposed) {
                callback.Dispose();
            }

            Logging.Warning(requestUrl);
            return false;
        }

        void IRequestHandler.OnPluginCrashed(IWebBrowser browserControl, IBrowser browser, string pluginPath) {
            Logging.Warning(pluginPath);
        }

        ICookieAccessFilter IResourceRequestHandler.GetCookieAccessFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request) {
            return this;
        }

        CefReturnValue IResourceRequestHandler.OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request,
                IRequestCallback callback) {
            if (RequestsFiltering.ShouldBeBlocked(request.Url)) {
                if (!callback.IsDisposed) {
                    callback.Dispose();
                }

                return CefReturnValue.Cancel;
            }

            if (UserAgent != null || Headers != null) {
                var headersCollection = request.Headers;
                var headers = new WebHeadersEventArgs(request.Url);
                Headers?.Invoke(this, headers);
                foreach (var pair in headers.Headers) {
                    headersCollection[pair.Key] = pair.Value;
                }
                if (UserAgent != null) {
                    headersCollection[@"User-Agent"] = UserAgent;
                }
                request.Headers = headersCollection;
            }

            return CefReturnValue.Continue;
        }

        IResourceHandler IResourceRequestHandler.GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request) {
            return null;
        }

        bool IRequestHandler.OnSelectClientCertificate(IWebBrowser browserControl, IBrowser browser, bool isProxy, string host, int port,
                X509Certificate2Collection certificates, ISelectClientCertificateCallback callback) {
            return true;
        }

        void IRequestHandler.OnRenderProcessTerminated(IWebBrowser browserControl, IBrowser browser, CefTerminationStatus status) {
            Logging.Warning(status);
        }

        bool IRequestHandler.OnQuotaRequest(IWebBrowser browserControl, IBrowser browser, string originUrl, long newSize, IRequestCallback callback) {
            if (!callback.IsDisposed) {
                callback.Dispose();
            }

            return true;
        }

        void IResourceRequestHandler.OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response,
                ref string newUrl) { }

        void IRequestHandler.OnRenderViewReady(IWebBrowser browserControl, IBrowser browser) { }

        bool IResourceRequestHandler.OnResourceResponse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response) {
            return false;
        }

        private static string GetColor(string key) {
            return Application.Current.TryFindResource(key) is SolidColorBrush b
                    ? $@"background:{b.Color.ToHexString()}!important;opacity:{b.Opacity}!important;" : "";
        }

        public event EventHandler<WebInjectEventArgs> Inject;

        public event EventHandler<WebHeadersEventArgs> Headers;

        private readonly string _windowColor = (Application.Current.TryFindResource(@"WindowBackgroundColor") as Color?)?.ToHexString();
        private readonly string _scrollThumbColor = GetColor(@"ScrollBarThumb");
        private readonly string _scrollThumbHoverColor = GetColor(@"ScrollBarThumbHover");
        private readonly string _scrollThumbDraggingColor = GetColor(@"ScrollBarThumbDragging");

        IResponseFilter IResourceRequestHandler.GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request,
                IResponse response) {
            if (response.MimeType == @"text/html") {
                var css = StyleProvider?.GetStyle(request.Url, browserControl is CefSharp.Wpf.ChromiumWebBrowser);
                var inject = new WebInjectEventArgs(request.Url);
                Inject?.Invoke(this, inject);
                return new ReplaceResponseFilter(inject.Replacements.Append(ReplaceResponseFilter.CreateCustomCssJs(inject.ToInject.JoinToString(), $@"
::-webkit-scrollbar {{ width: 8px!important; height: 8px!important; }}
::-webkit-scrollbar-track {{ box-shadow: none!important; border-radius: 0!important; background: {_windowColor}!important; opacity: 0!important; }}
::-webkit-scrollbar-corner {{ background: {_windowColor}!important; }}
::-webkit-scrollbar-thumb {{ border: none!important; box-shadow: none!important; border-radius: 0!important; {_scrollThumbColor} }}
::-webkit-scrollbar-thumb:hover {{ {_scrollThumbHoverColor} }}
::-webkit-scrollbar-thumb:active {{ {_scrollThumbDraggingColor} }}\n" + css, @"
(function(){
})()")));
            }

            return null;
        }

        void IResourceRequestHandler.OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response,
                UrlRequestStatus status, long receivedContentLength) { }

        bool IResourceRequestHandler.OnProtocolExecution(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request) {
            return request.Url.StartsWith(@"mailto") || request.Url.StartsWith(@"acmanager");
        }

        private class ReplaceResponseFilter : StreamReplacement, IResponseFilter {
            public static KeyValuePair<string, string> CreateCustomCssJs(string prefix, string css, string js) {
                return new KeyValuePair<string, string>(@"</head>", $@"{prefix}<style>{css}</style><script>{js}</script></head>");
            }

            public ReplaceResponseFilter(IEnumerable<KeyValuePair<string, string>> replacements) : base(replacements) { }

            bool IResponseFilter.InitFilter() {
                return true;
            }

            FilterStatus IResponseFilter.Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten) {
                return Filter(dataIn, out dataInRead, dataOut, out dataOutWritten) ? FilterStatus.Done : FilterStatus.NeedMoreData;
            }

            void IDisposable.Dispose() { }
        }

        bool ICookieAccessFilter.CanSendCookie(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, Cookie cookie) {
            return true;
        }

        bool ICookieAccessFilter.CanSaveCookie(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response,
                Cookie cookie) {
            return true;
        }

        void IDisposable.Dispose() { }
    }
}