using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using CefSharp;
using CefSharp.WinForms;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Size = CefSharp.Structs.Size;
using TitleChangedEventArgs = CefSharp.TitleChangedEventArgs;

namespace AcManager.Controls.UserControls.Cef {
    internal class FormsCefSharpWrapper : IWebSomething, IDisplayHandler {
        [CanBeNull]
        private WindowsFormsHost _wrapper;

        [CanBeNull]
        private ChromiumWebBrowser _inner;

        public FormsCefSharpWrapper() {
            ForwardCommand = new DelegateCommand(() => _inner?.Forward(), () => _inner?.CanGoForward == true);
            BackCommand = new DelegateCommand(() => _inner?.Back(), () => _inner?.CanGoBack == true);
        }

        private RequestHandler _requestHandler;
        private DownloadHandler _downloadHandler;

        public FrameworkElement GetElement(DpiAwareWindow parentWindow, bool preferTransparentBackground) {
            if (_inner == null || _wrapper == null) {
                DisposeHelper.Dispose(ref _inner);
                DisposeHelper.Dispose(ref _wrapper);
                CefSharpHelper.EnsureInitialized();

                _downloadHandler = new DownloadHandler();
                _requestHandler = new RequestHandler { UserAgent = CefSharpHelper.DefaultUserAgent };
                _requestHandler.Inject += OnRequestHandlerInject;
                _requestHandler.Headers += OnRequestHandlerHeaders;

                _inner = new ChromiumWebBrowser(@"about:blank") {
                    BrowserSettings = {
                        FileAccessFromFileUrls = CefState.Enabled,
                        UniversalAccessFromFileUrls = CefState.Enabled,
                        BackgroundColor = 0xff000000,

                        // For SRS to work, because IsCSPBypassing somehow doesn’t work!
                        WebSecurity = CefState.Disabled,
                    },
                    DisplayHandler = this,
                    DownloadHandler = _downloadHandler,
                    RequestHandler = _requestHandler,
                    KeyboardHandler = new KeyboardHandler()
                    // MenuHandler = new MenuHandler(),
                    // JsDialogHandler = new JsDialogHandler(),
                };

                _inner.FrameLoadStart += OnFrameLoadStart;
                _inner.FrameLoadEnd += OnFrameLoadEnd;
                _inner.LoadingStateChanged += OnLoadingStateChanged;
                _inner.LoadError += OnLoadError;
                _wrapper = new WindowsFormsHost { Child = _inner };
            }

            return _wrapper;
        }

        private string AlterUrl([CanBeNull] string url) {
            if (url?.StartsWith(@"data:") == true) {
                return _attemptedToNavigateTo ?? url;
            }
            return url ?? string.Empty;
        }

        private void OnLoadError(object sender, LoadErrorEventArgs args) {
            if (args.ErrorCode == CefErrorCode.Aborted || _inner == null) return;
            if (args.ErrorCode == CefErrorCode.NameNotResolved && args.FailedUrl == @"http://" + _attemptedToNavigateTo + @"/") {
                args.Frame.LoadUrl(SettingsHolder.Content.SearchEngine.GetUrl(_attemptedToNavigateTo, false));
                return;
            }

            var html = $@"<html><body bgcolor=""white"" style=""font-family:segoe ui, sans-serif;"">
<h2>Failed to load URL {args.FailedUrl}</h2>
<p>Error: {args.ErrorText} ({args.ErrorCode}).</p>
</body></html>";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
            _inner.Load("data:text/html;base64," + encoded);
        }

        private void OnFrameLoadStart(object sender, FrameLoadStartEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThread(() => PageLoadingStarted?.Invoke(this, new UrlEventArgs(e.Url ?? string.Empty)));
            }
        }

        private void OnLoadingStateChanged(object sender, LoadingStateChangedEventArgs e) {
            ActionExtension.InvokeInMainThreadAsync(() => LoadingStateChanged?.Invoke(this, e.IsLoading
                    ? new PageLoadingEventArgs(AsyncProgressEntry.Indetermitate, _inner?.Address)
                    : new PageLoadingEventArgs(AsyncProgressEntry.Ready, _inner?.Address)));
        }

        private string OnAcApiRequest(string url) {
            var e = new AcApiRequestEventArgs(url);
            AcApiRequest?.Invoke(this, e);
            return e.Response;
        }

        private void OnRequestHandlerInject(object o, WebInjectEventArgs webInjectEventArgs) {
            Inject?.Invoke(this, webInjectEventArgs);
        }

        private void OnRequestHandlerHeaders(object o, WebHeadersEventArgs webInjectEventArgs) {
            Headers?.Invoke(this, webInjectEventArgs);
        }

        public event EventHandler<PageLoadingEventArgs> LoadingStateChanged;
        public event EventHandler<UrlEventArgs> PageLoadingStarted;
        public event EventHandler<UrlEventArgs> PageLoaded;
        public event EventHandler<NewWindowEventArgs> NewWindow;
        public event EventHandler<Web.TitleChangedEventArgs> TitleChanged;
        public event EventHandler<UrlEventArgs> AddressChanged;
        public event EventHandler<FaviconChangedEventArgs> FaviconChanged;

        public bool SupportsFavicons => true;

        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThread(() => PageLoaded?.Invoke(this, new UrlEventArgs(e.Url ?? string.Empty)));
            }
        }

        public string GetUrl() {
            return AlterUrl(_inner?.Address);
        }

        private bool _jsBridgeSet;

        [CanBeNull]
        private JsBridgeBase _jsBridge;

        public T GetJsBridge<T>(Func<T> factory) where T : JsBridgeBase {
            if (_jsBridgeSet) {
                return (T)_jsBridge;
            }

            _jsBridge = factory();
            _jsBridgeSet = true;

            try {
                if (_inner != null) {
                    CefSharpHelper.AcApiHandler.Register(_inner, _jsBridge?.AcApiHosts.ToArray(), OnAcApiRequest);
                    _inner.JavascriptObjectRepository.Register(@"external", _jsBridge, false, new BindingOptions {
                        Binder = BindingOptions.DefaultBinder.Binder,
                        CamelCaseJavascriptNames = false
                    });
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            return (T)_jsBridge;
        }

        public void SetUserAgent(string userAgent) {
            _requestHandler.UserAgent = userAgent;
        }

        public void SetStyleProvider(ICustomStyleProvider provider) {
            _requestHandler.StyleProvider = provider;
        }

        public void SetDownloadListener(IWebDownloadListener listener) {
            _downloadHandler.Listener = listener;
        }

        public void SetNewWindowsBehavior(NewWindowsBehavior mode) {
            if (_inner == null) return;
            _inner.LifeSpanHandler = new LifeSpanHandler(mode, url => {
                var args = new NewWindowEventArgs(url);
                NewWindow?.Invoke(this, args);
                return args.Cancel;
            });
        }

        public void Execute(string js) {
#if DEBUG
            Logging.Debug(js);
#endif

            if (_inner?.IsBrowserInitialized != true) {
                Logging.Warning("Browser is not initialized yet!");
                return;
            }

            try {
                using (var mainFrame = _inner.GetMainFrame()) {
                    mainFrame.ExecuteJavaScriptAsync(js, @"about:contentmanager");
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private string _attemptedToNavigateTo;

        public void Navigate(string url) {
            if (Equals(url, GetUrl())) {
                _inner?.Reload(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                return;
            }

            _attemptedToNavigateTo = url;
            _inner?.Load(url);
        }

        public bool CanHandleAcApiRequests => true;

        public event EventHandler<AcApiRequestEventArgs> AcApiRequest;

        public bool IsInjectSupported => true;

        public event EventHandler<WebInjectEventArgs> Inject;

        public bool AreHeadersSupported => true;

        public event EventHandler<WebHeadersEventArgs> Headers;

        public bool CanConvertFilenames => true;

        public string ConvertFilename(string filename) {
            return filename == null ? null : new Uri(filename, UriKind.Absolute).AbsoluteUri.Replace(@"file", AltFilesHandlerFactory.SchemeName);
        }

        public ICommand BackCommand { get; }
        public ICommand ForwardCommand { get; }

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(
                noCache => _inner.Reload(noCache == true)));

        public void OnLoaded() { }

        public void OnUnloaded() {
            if (_downloadHandler.IsAnyDownloadActive) {
                _downloadHandler.PropertyChanged += OnDownloadHandlerPropertyChanged;
            } else {
                DisposeHelper.Dispose(ref _inner);
                DisposeHelper.Dispose(ref _wrapper);
            }

            void OnDownloadHandlerPropertyChanged(object sender, PropertyChangedEventArgs args) {
                if (args.PropertyName == nameof(_downloadHandler.IsAnyDownloadActive) && !_downloadHandler.IsAnyDownloadActive) {
                    _downloadHandler.PropertyChanged -= OnDownloadHandlerPropertyChanged;
                    DisposeHelper.Dispose(ref _inner);
                    DisposeHelper.Dispose(ref _wrapper);
                }
            }
        }

        public void OnError(string error, string url, int line, int column) { }

        Task<string> IWebSomething.GetImageUrlAsync(string filename) {
            return Task.FromResult(ConvertFilename(filename));
        }

        void IDisplayHandler.OnAddressChanged(IWebBrowser browserControl, AddressChangedEventArgs args) {
            ActionExtension.InvokeInMainThreadAsync(() => AddressChanged?.Invoke(this, new UrlEventArgs(args.Address ?? string.Empty)));
        }

        public bool OnAutoResize(IWebBrowser browserControl, IBrowser browser, Size newSize) {
            return false;
        }

        void IDisplayHandler.OnTitleChanged(IWebBrowser browserControl, TitleChangedEventArgs args) {
            ActionExtension.InvokeInMainThreadAsync(() => TitleChanged?.Invoke(this, new Web.TitleChangedEventArgs(args.Title ?? string.Empty)));
        }

        void IDisplayHandler.OnFaviconUrlChange(IWebBrowser browserControl, IBrowser browser, IList<string> urls) {
            ActionExtension.InvokeInMainThreadAsync(() => FaviconChanged?.Invoke(this, new FaviconChangedEventArgs(urls.FirstOrDefault())));
        }

        void IDisplayHandler.OnFullscreenModeChange(IWebBrowser browserControl, IBrowser browser, bool fullscreen) { }

        public void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress) {
            // TODO
        }

        bool IDisplayHandler.OnTooltipChanged(IWebBrowser browserControl, ref string text) {
            return false;
        }

        void IDisplayHandler.OnStatusMessage(IWebBrowser browserControl, StatusMessageEventArgs statusMessageArgs) { }

        bool IDisplayHandler.OnConsoleMessage(IWebBrowser browserControl, ConsoleMessageEventArgs consoleMessageArgs) {
            return true;
        }
    }
}