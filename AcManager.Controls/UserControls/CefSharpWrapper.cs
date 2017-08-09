using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
using CefSharp;
using CefSharp.Wpf;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using LogSeverity = CefSharp.LogSeverity;

namespace AcManager.Controls.UserControls {
    /// <summary>
    /// Vague attempt to improve poor performance of those Chromium-based engines
    /// at least a little bit.
    /// </summary>
    internal static class RequestsFiltering {
        private static readonly Regex Regex = new Regex(@"^
                https?://(?:
                    googleads\.g\.doubleclick\.net/ |
                    apis\.google\.com/se/0/_/\+1 |
                    pagead2\.googlesyndication\.com/pagead |
                    staticxx\.facebook\.com/connect |
                    syndication\.twitter\.com/i/jot |
                    platform\.twitter\.com/widgets |
                    www\.youtube\.com/subscribe_embed |
                    www\.facebook\.com/connect/ping |
                    www\.facebook\.com/plugins/like\.php )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static bool ShouldBeBlocked(string url) {
            if (!SettingsHolder.Plugins.CefFilterAds) return false;

#if DEBUG
            // Logging.Debug(url);
#endif
            return Regex.IsMatch(url);
        }
    }

    internal class MenuHandler : IContextMenuHandler {
        public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model) {}

        public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId,
                CefEventFlags eventFlags) {
            return false;
        }

        public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame) {}

        public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model,
                IRunContextMenuCallback callback) {
            callback.Dispose();

            ActionExtension.InvokeInMainThread(() => {
                var menu = new ContextMenu {
                    Items = {
                        new MenuItem {
                            Header = "Back",
                            Command = new DelegateCommand(browser.GoBack, () => browser.CanGoBack)
                        },
                        new MenuItem {
                            Header = "Forward",
                            Command = new DelegateCommand(browser.GoForward, () => browser.CanGoForward)
                        },
                        new MenuItem {
                            Header = "Refresh",
                            Command = new DelegateCommand(() => browser.Reload(true))
                        },
                        new Separator(),
                        new MenuItem {
                            Header = "Select All",
                            Command = new DelegateCommand(() => browser.FocusedFrame.SelectAll())
                        },
                        new MenuItem {
                            Header = "Open Page In Default Browser",
                            Command = new DelegateCommand<string>(WindowsHelper.ViewInBrowser),
                            CommandParameter = frame.Url
                        },
                    }
                };

                menu.IsOpen = true;
            });

            return true;
        }
    }

    internal class RequestHandler : IRequestHandler {
        [CanBeNull]
        internal string UserAgent { get; set; }

        [CanBeNull]
        internal ICustomStyleProvider StyleProvider { get; set; }

        public bool OnBeforeBrowse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, bool isRedirect) {
            // if (request.TransitionType.HasFlag(TransitionType.ForwardBack)) return true;
            return RequestsFiltering.ShouldBeBlocked(request.Url);
        }

        public bool OnOpenUrlFromTab(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture) {
            return false;
        }

        public bool OnCertificateError(IWebBrowser browserControl, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback) {
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

        public bool GetAuthCredentials(IWebBrowser browserControl, IBrowser browser, IFrame frame, bool isProxy, string host, int port, string realm, string scheme,
                IAuthCallback callback) {
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

        public void OnResourceRedirect(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, ref string newUrl) {}

        public bool OnProtocolExecution(IWebBrowser browserControl, IBrowser browser, string url) {
            return url.StartsWith(@"mailto") || url.StartsWith(@"acmanager");
        }

        public void OnRenderViewReady(IWebBrowser browserControl, IBrowser browser) {}

        public bool OnResourceResponse(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response) {
            return false;
        }

        public IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response) {
            if (response.MimeType == @"text/html" && StyleProvider != null) {
                var css = StyleProvider.GetStyle(request.Url);
                return ReplaceResponseFilter.CreateCustomCss(@"
::-webkit-scrollbar { width: 8px!important; height: 8px!important; }
::-webkit-scrollbar-track { box-shadow: none!important; border-radius: 0!important; background: #000!important; }
::-webkit-scrollbar-corner { background: #000 !important; }
::-webkit-scrollbar-thumb { border: none !important; box-shadow: none !important; border-radius: 0 !important; background: #333 !important; }
::-webkit-scrollbar-thumb:hover { background: #444 !important; }
::-webkit-scrollbar-thumb:active { background: #666 !important; }", css);
            }

            return null;
        }

        public void OnResourceLoadComplete(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status,
                long receivedContentLength) {}

        private class ReplaceResponseFilter : IResponseFilter {
            private static readonly Encoding Encoding = Encoding.UTF8;

            private readonly byte[] _find;
            private readonly byte[] _replacement;

            private readonly List<byte> _overflow = new List<byte>();
            private int _findMatchOffset;

            public static ReplaceResponseFilter CreateCustomCss(params string[] css) {
                return new ReplaceResponseFilter(@"</head>", $@"<style>{css.JoinToString('\n')}</style></head>");
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

            public void Dispose() {}
        }
    }

    public class AltFilesHandlerFactory : ISchemeHandlerFactory {
        public const string SchemeName = "custom";

        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request) {
            if (schemeName == SchemeName) {
                var slice = SchemeName.Length + 4;
                if (slice >= request.Url.Length) return null;

                var filename = $@"{request.Url[slice - 1].ToInvariantString()}:{request.Url.Substring(slice)}";
                var mimeType = ResourceHandler.GetMimeType(Path.GetExtension(filename));
                return ResourceHandler.FromFilePath(filename, mimeType);
            }

            return null;
        }
    }

    internal class CefSharpWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static CefSharpWrapper() {
            var windows = $@"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent =
                    $@"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        private ChromiumWebBrowser _inner;
        private RequestHandler _requestHandler;

        public FrameworkElement Initialize() {
            if (!Cef.IsInitialized) {
                var path = PluginsManager.Instance.GetPluginDirectory("CefSharp");
                var settings = new CefSettings {
                    UserAgent = DefaultUserAgent,
                    MultiThreadedMessageLoop = true,
                    LogSeverity = LogSeverity.Disable,
                    CachePath = FilesStorage.Instance.GetTemporaryFilename(@"Cef"),
                    UserDataPath = FilesStorage.Instance.GetTemporaryFilename(@"Cef"),
                    BrowserSubprocessPath = Path.Combine(path, "CefSharp.BrowserSubprocess.exe"),
                    LocalesDirPath = Path.Combine(path, "locales"),
                    ResourcesDirPath = Path.Combine(path),
                    Locale = SettingsHolder.Locale.LocaleName,
#if DEBUG
                    RemoteDebuggingPort = 45451,
#endif
                };

                settings.RegisterScheme(new CefCustomScheme {
                    SchemeName = AltFilesHandlerFactory.SchemeName,
                    SchemeHandlerFactory = new AltFilesHandlerFactory()
                });

                AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                    try {
                        Cef.Shutdown();
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                };

                Cef.Initialize(settings, false, null);
            }

            _requestHandler = new RequestHandler {
                UserAgent = DefaultUserAgent
            };

            _inner = new ChromiumWebBrowser {
                BrowserSettings = {
                    FileAccessFromFileUrls = CefState.Enabled,
                    UniversalAccessFromFileUrls = CefState.Enabled,
                    WebSecurity = CefState.Disabled
                },
                RequestHandler = _requestHandler,
                MenuHandler = new MenuHandler()
            };

            _inner.FrameLoadStart += OnFrameLoadStart;
            _inner.FrameLoadEnd += OnFrameLoadEnd;
            return _inner;
        }

        public event EventHandler<PageLoadingEventArgs> Navigating;

        public event EventHandler<PageLoadedEventArgs> Navigated;

        private void OnFrameLoadStart(object sender, FrameLoadStartEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThreadAsync(() => {
                    Navigating?.Invoke(this, PageLoadingEventArgs.Indetermitate);
                });
            }
        }

        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThread(() => {
                    Navigating?.Invoke(this, PageLoadingEventArgs.Ready);

                    ModifyPage();
                    Navigated?.Invoke(this, new PageLoadedEventArgs(_inner.Address));
                });
            }
        }

        public string GetUrl() {
            return _inner.Address;
        }

        public void SetScriptProvider(ScriptProviderBase provider) {
            _inner.RegisterJsObject(@"external", provider, new BindingOptions { CamelCaseJavascriptNames = false });
        }

        public void SetUserAgent(string userAgent) {
            _requestHandler.UserAgent = userAgent;
        }

        public void SetStyleProvider(ICustomStyleProvider provider) {
            _requestHandler.StyleProvider = provider;
        }

        public void ModifyPage() {
            Execute(@"window.__cm_loaded = true;
window.onerror = function(error, url, line, column){ window.external.OnError(error, url, line, column); };
document.addEventListener('mousedown', function(e){
    var t = e.target;
    if (t.tagName != 'A' || !t.href) return;

    if (t.href.indexOf(location.host) !== -1){
        if (t.getAttribute('target') == '_blank') t.setAttribute('target', '_parent');
    } else if (t.getAttribute('__cm_added') != 'y'){
        t.setAttribute('__cm_added', 'y');
        t.addEventListener('click', function(ev){
            if (ev.which == 1){
                window.external.NavigateTo(this.href);
                ev.preventDefault();
                ev.stopPropagation();
            }
        });
    }
}, false);");
        }

        public void Execute(string js) {
            using (var mainFrame = _inner.GetMainFrame()) {
                mainFrame.ExecuteJavaScriptAsync(js, @"about:contentmanager");
            }
        }

        public void Navigate(string url) {
            if (Equals(url, GetUrl())) {
                _inner.Reload(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                return;
            }

            _inner.Address = url;
        }

        public ICommand BackCommand => _inner.BackCommand;

        public ICommand ForwardCommand => _inner.ForwardCommand;

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(noCache => {
            _inner.Reload(noCache == true);
        }));

        public void OnLoaded() {}

        public void OnUnloaded() {}

        public void OnError(string error, string url, int line, int column) {}

        public Task<string> GetImageUrlAsync(string filename) {
            return Task.FromResult(filename == null ? null : new Uri(filename, UriKind.Absolute).AbsoluteUri.Replace(@"file", AltFilesHandlerFactory.SchemeName));
        }
    }
}