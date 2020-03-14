using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using CefSharp;
using CefSharp.Wpf;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Size = CefSharp.Structs.Size;
using TitleChangedEventArgs = CefSharp.TitleChangedEventArgs;

namespace AcManager.Controls.UserControls.Cef {
    internal class CefSharpWrapper : IWebSomething, IDisplayHandler {
        private Border _wrapper;

        [CanBeNull]
        private ChromiumWebBrowser _inner;

        private RequestHandler _requestHandler;
        private DownloadHandler _downloadHandler;
        private double _zoomLevel;

        public FrameworkElement GetElement(DpiAwareWindow parentWindow, bool preferTransparentBackground) {
            if (_inner == null) {
                CefSharpHelper.EnsureInitialized();

                _downloadHandler = new DownloadHandler();
                _requestHandler = new RequestHandler { UserAgent = CefSharpHelper.DefaultUserAgent };
                _requestHandler.Inject += OnRequestHandlerInject;

                _inner = new ChromiumWebBrowser {
                    BrowserSettings = {
                        FileAccessFromFileUrls = CefState.Enabled,
                        UniversalAccessFromFileUrls = CefState.Enabled,
                        BackgroundColor = preferTransparentBackground ? 0U : 0xffffffff,
                        WindowlessFrameRate = SettingsHolder.Plugins.Cef60Fps ? 60 : 30,
                        WebGl = CefState.Disabled,
                        Plugins = CefState.Disabled,

                        // For SRS to work, because IsCSPBypassing somehow doesn’t work!
                        WebSecurity = CefState.Disabled,
                    },
                    DisplayHandler = this,
                    DownloadHandler = _downloadHandler,
                    RequestHandler = _requestHandler,
                    MenuHandler = new MenuHandler(),
                    JsDialogHandler = new JsDialogHandler(),
                    KeyboardHandler = new KeyboardHandler(),
                    Background = new SolidColorBrush(preferTransparentBackground ? Colors.Transparent : Colors.White),
                };

                RenderOptions.SetBitmapScalingMode(_inner, BitmapScalingMode.NearestNeighbor);
                _inner.FrameLoadStart += OnFrameLoadStart;
                _inner.FrameLoadEnd += OnFrameLoadEnd;
                _inner.LoadingStateChanged += OnLoadingStateChanged;
                _inner.LoadError += OnLoadError;
                _wrapper = new Border { Child = _inner };
            }

            _zoomLevel = parentWindow?.ScaleX ?? 1d;
            if (_zoomLevel > 1d) {
                _inner.LayoutTransform = new ScaleTransform { ScaleX = 1d / _zoomLevel, ScaleY = 1d / _zoomLevel };
                _inner.SetBinding(FrameworkElement.WidthProperty, new Binding {
                    Path = new PropertyPath(FrameworkElement.ActualWidthProperty),
                    Source = _wrapper,
                    Converter = new MultiplyConverter(),
                    ConverterParameter = _zoomLevel
                });
                _inner.SetBinding(FrameworkElement.HeightProperty, new Binding {
                    Path = new PropertyPath(FrameworkElement.ActualHeightProperty),
                    Source = _wrapper,
                    Converter = new MultiplyConverter(),
                    ConverterParameter = _zoomLevel
                });
            } else {
                _inner.LayoutTransform = Transform.Identity;
                _inner.ClearValue(FrameworkElement.WidthProperty);
                _inner.ClearValue(FrameworkElement.HeightProperty);
            }

            RenderOptions.SetBitmapScalingMode(_inner, _zoomLevel >= 1d ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            return _wrapper;
        }

        private void OnLoadError(object sender, LoadErrorEventArgs args) {
            if (args.ErrorCode == CefErrorCode.Aborted) return;
            if (args.ErrorCode == CefErrorCode.NameNotResolved && args.FailedUrl == @"http://" + _attemptedToNavigateTo + @"/") {
                args.Frame.LoadUrl(SettingsHolder.Content.SearchEngine.GetUrl(_attemptedToNavigateTo, false));
                return;
            }

            args.Frame.LoadHtml($@"<html><body bgcolor=""white"" style=""font-family:segoe ui, sans-serif;"">
<h2>Failed to load URL {args.FailedUrl}</h2>
<p>Error: {args.ErrorText} ({args.ErrorCode}).</p>
</body></html>");
        }

        private void OnFrameLoadStart(object sender, FrameLoadStartEventArgs e) {
            _inner.SetZoomLevel(Math.Log(_zoomLevel, 1.2));
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
            return _inner?.Address ?? string.Empty;
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
            if (_inner != null) {
                _inner.Address = url;
            }
        }

        public bool CanHandleAcApiRequests => true;
        public event EventHandler<AcApiRequestEventArgs> AcApiRequest;

        public bool IsInjectSupported => true;
        public event EventHandler<WebInjectEventArgs> Inject;

        public bool CanConvertFilenames => true;

        private static readonly Regex PathUrlFix = new Regex("^file(?=://)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string ConvertFilename(string filename) {
            return filename == null ? null : PathUrlFix.Replace(new Uri(filename, UriKind.Absolute).AbsoluteUri, AltFilesHandlerFactory.SchemeName);
        }

        public ICommand BackCommand => _inner?.BackCommand ?? UnavailableCommand.Instance;
        public ICommand ForwardCommand => _inner?.ForwardCommand ?? UnavailableCommand.Instance;

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(
                noCache => _inner.Reload(noCache == true)));

        public void OnLoaded() { }

        public void OnUnloaded() {
            if (_downloadHandler.IsAnyDownloadActive) {
                _downloadHandler.PropertyChanged += OnDownloadHandlerPropertyChanged;
            } else {
                DisposeHelper.Dispose(ref _inner);
            }

            void OnDownloadHandlerPropertyChanged(object sender, PropertyChangedEventArgs args) {
                if (args.PropertyName == nameof(_downloadHandler.IsAnyDownloadActive) && !_downloadHandler.IsAnyDownloadActive) {
                    _downloadHandler.PropertyChanged -= OnDownloadHandlerPropertyChanged;
                    DisposeHelper.Dispose(ref _inner);
                }
            }
        }

        public void OnError(string error, string url, int line, int column) { }

        public Task<string> GetImageUrlAsync(string filename) {
            return Task.FromResult(ConvertFilename(filename));
        }

        void IDisplayHandler.OnAddressChanged(IWebBrowser browserControl, AddressChangedEventArgs args) {
            ActionExtension.InvokeInMainThreadAsync(() => AddressChanged?.Invoke(this, new UrlEventArgs(args.Address ?? string.Empty)));
        }

        bool IDisplayHandler.OnAutoResize(IWebBrowser browserControl, IBrowser browser, Size newSize) {
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