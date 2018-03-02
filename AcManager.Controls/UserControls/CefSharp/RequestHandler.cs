using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.UserControls.Web;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.CefSharp {
    internal class RequestHandler : IRequestHandler {
        [CanBeNull]
        internal string UserAgent { get; set; }

        [CanBeNull]
        internal ICustomStyleProvider StyleProvider { get; set; }

        public bool OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool isRedirect) {
            // if (request.TransitionType.HasFlag(TransitionType.ForwardBack)) return true;
            return RequestsFiltering.ShouldBeBlocked(request.Url);
        }

        public bool OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition,
                bool userGesture) {
            return false;
        }

        public bool OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo,
                IRequestCallback callback) {
            if (!callback.IsDisposed) {
                callback.Dispose();
            }

            Logging.Warning(requestUrl);
            return false;
        }

        public void OnPluginCrashed(IWebBrowser browserControl, IBrowser browser, string pluginPath) {
            Logging.Warning(pluginPath);
        }

        public CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback) {
            if (RequestsFiltering.ShouldBeBlocked(request.Url)) {
                if (!callback.IsDisposed) {
                    callback.Dispose();
                }

                return CefReturnValue.Cancel;
            }

            if (UserAgent != null) {
                var headers = request.Headers;
                headers[@"User-Agent"] = UserAgent;
                request.Headers = headers;
            }

            return CefReturnValue.Continue;
        }

        public bool GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, IFrame frame, bool isProxy, string host, int port, string realm,
                string scheme, IAuthCallback callback) {
            return true;
        }

        public bool OnSelectClientCertificate(IWebBrowser browserControl, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates,
                ISelectClientCertificateCallback callback) {
            return true;
        }

        public void OnRenderProcessTerminated(IWebBrowser browserControl, IBrowser browser, CefTerminationStatus status) {
            Logging.Warning(status);
        }

        public bool OnQuotaRequest(IWebBrowser browserControl, IBrowser browser, string originUrl, long newSize, IRequestCallback callback) {
            if (!callback.IsDisposed) {
                callback.Dispose();
            }

            return true;
        }

        public void OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl) {}

        public bool OnProtocolExecution(IWebBrowser browserControl, IBrowser browser, string url) {
            return url.StartsWith(@"mailto") || url.StartsWith(@"acmanager");
        }

        public void OnRenderViewReady(IWebBrowser browserControl, IBrowser browser) { }

        public bool OnResourceResponse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response) {
            return false;
        }

        private static string GetColor(string key) {
            return Application.Current.TryFindResource(key) is SolidColorBrush b
                    ? $@"background:{b.Color.ToHexString()}!important;opacity:{b.Opacity}!important;" : "";
        }

        public event EventHandler<WebInjectEventArgs> Inject;

        private readonly string _windowColor = (Application.Current.TryFindResource(@"WindowBackgroundColor") as Color?)?.ToHexString();
        private readonly string _scrollThumbColor = GetColor(@"ScrollBarThumb");
        private readonly string _scrollThumbHoverColor = GetColor(@"ScrollBarThumbHover");
        private readonly string _scrollThumbDraggingColor = GetColor(@"ScrollBarThumbDragging");

        public IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response) {
            if (response.MimeType == @"text/html") {
                var css = StyleProvider?.GetStyle(request.Url);
                var inject = new WebInjectEventArgs(request.Url);
                Inject?.Invoke(this, inject);
                return ReplaceResponseFilter.CreateCustomCss(inject.ToInject.JoinToString(), $@"
::-webkit-scrollbar {{ width: 8px!important; height: 8px!important; }}
::-webkit-scrollbar-track {{ box-shadow: none!important; border-radius: 0!important; background: {_windowColor}!important; opacity: 0!important; }}
::-webkit-scrollbar-corner {{ background: {_windowColor}!important; }}
::-webkit-scrollbar-thumb {{ border: none!important; box-shadow: none!important; border-radius: 0!important; {_scrollThumbColor} }}
::-webkit-scrollbar-thumb:hover {{ {_scrollThumbHoverColor} }}
::-webkit-scrollbar-thumb:active {{ {_scrollThumbDraggingColor} }}", css);
            }

            return null;
        }

        public void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response,
                UrlRequestStatus status, long receivedContentLength) { }

        private class ReplaceResponseFilter : IResponseFilter {
            private static readonly Encoding Encoding = Encoding.UTF8;

            private readonly byte[] _find;
            private readonly byte[] _replacement;

            private readonly List<byte> _overflow = new List<byte>();
            private int _findMatchOffset;

            public static ReplaceResponseFilter CreateCustomCss(string prefix, params string[] css) {
                return new ReplaceResponseFilter(@"</head>", $@"{prefix}<style>{css.NonNull().JoinToString('\n')}</style></head>");
            }

            public ReplaceResponseFilter(string find, string replace) {
                _find = Encoding.GetBytes(find);
                _replacement = Encoding.GetBytes(replace);
            }

            bool IResponseFilter.InitFilter() {
                return true;
            }

            FilterStatus IResponseFilter.Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten) {
                dataOutWritten = 0;

                if (dataIn == null) {
                    dataInRead = 0;
                    return FilterStatus.Done;
                }

                dataInRead = dataIn.Length;
                if (_overflow.Count > 0) {
                    WriteOverflow(dataOut, ref dataOutWritten);
                }

                for (var i = 0; i < dataInRead; ++i) {
                    var readByte = (byte)dataIn.ReadByte();
                    if (readByte != _find[_findMatchOffset]) {
                        if (_findMatchOffset > 0) {
                            WriteBytes(_find, _findMatchOffset, dataOut, ref dataOutWritten);
                            _findMatchOffset = 0;
                        }

                        WriteSingleByte(readByte, dataOut, ref dataOutWritten);
                    } else if (++_findMatchOffset == _find.Length) {
                        WriteBytes(_replacement, _replacement.Length, dataOut, ref dataOutWritten);
                        _findMatchOffset = 0;
                    }
                }

                return _overflow.Count > 0 ? FilterStatus.NeedMoreData :
                        _findMatchOffset > 0 ? FilterStatus.NeedMoreData : FilterStatus.Done;
            }

            private void WriteOverflow(Stream dataOut, ref long dataOutWritten) {
                var remainingSpace = dataOut.Length - dataOutWritten;
                var maxWrite = Math.Min(_overflow.Count, remainingSpace);

                if (maxWrite > 0) {
                    dataOut.Write(_overflow.ToArray(), 0, (int)maxWrite);
                    dataOutWritten += maxWrite;
                }

                if (maxWrite < _overflow.Count) {
                    _overflow.RemoveRange(0, (int)(maxWrite - 1));
                } else {
                    _overflow.Clear();
                }
            }

            private void WriteBytes(byte[] bytes, int bytesCount, Stream dataOut, ref long dataOutWritten) {
                var remainingSpace = dataOut.Length - dataOutWritten;
                var maxWrite = Math.Min(bytesCount, remainingSpace);

                if (maxWrite > 0) {
                    dataOut.Write(bytes, 0, (int)maxWrite);
                    dataOutWritten += maxWrite;
                }

                if (maxWrite < bytesCount) {
                    var range = new byte[bytesCount - maxWrite];
                    Array.Copy(bytes, maxWrite, range, 0, range.LongLength);
                    _overflow.AddRange(range);
                }
            }

            private void WriteSingleByte(byte data, Stream dataOut, ref long dataOutWritten) {
                var remainingSpace = dataOut.Length - dataOutWritten;

                if (remainingSpace > 0) {
                    dataOut.WriteByte(data);
                    dataOutWritten += 1;
                } else {
                    _overflow.Add(data);
                }
            }

            public void Dispose() { }
        }
    }
}