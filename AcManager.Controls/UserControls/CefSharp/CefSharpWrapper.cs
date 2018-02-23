using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcTools;
using CefSharp;
using CefSharp.Wpf;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using LogSeverity = CefSharp.LogSeverity;

namespace AcManager.Controls.UserControls.CefSharp {
    internal class CefSharpWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static CefSharpWrapper() {
            var windows = $@"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent = $@"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        private Border _wrapper;
        private ChromiumWebBrowser _inner;
        private RequestHandler _requestHandler;
        private DownloadHandler _downloadHandler;
        private double _zoomLevel;

        private static readonly AcApiHandlerFactory AcApiHandler = new AcApiHandlerFactory();

        public FrameworkElement GetElement(DpiAwareWindow parentWindow, bool preferTransparentBackground) {
            if (_inner == null) {
                if (!Cef.IsInitialized) {
                    var path = PluginsManager.Instance.GetPluginDirectory(KnownPlugins.CefSharp);
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
                        SchemeName = AcApiHandlerFactory.AcSchemeName,
                        SchemeHandlerFactory = AcApiHandler
                    });

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

                _downloadHandler = new DownloadHandler();
                _requestHandler = new RequestHandler { UserAgent = DefaultUserAgent };
                _requestHandler.Inject += OnRequestHandlerInject;

                _inner = new ChromiumWebBrowser {
                    BrowserSettings = {
                        FileAccessFromFileUrls = CefState.Enabled,
                        UniversalAccessFromFileUrls = CefState.Enabled,
                        WebSecurity = CefState.Disabled,
                        OffScreenTransparentBackground = preferTransparentBackground,
                    },
                    DownloadHandler = _downloadHandler,
                    RequestHandler = _requestHandler,
                    MenuHandler = new MenuHandler(),
                };

                RenderOptions.SetBitmapScalingMode(_inner, BitmapScalingMode.NearestNeighbor);
                _inner.FrameLoadStart += OnFrameLoadStart;
                _inner.FrameLoadEnd += OnFrameLoadEnd;
                _inner.TitleChanged += OnTitleChanged;

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
                _inner.LayoutTransform = null;
                _inner.ClearValue(FrameworkElement.WidthProperty);
                _inner.ClearValue(FrameworkElement.HeightProperty);
            }

            RenderOptions.SetBitmapScalingMode(_inner, _zoomLevel >= 1d ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            return _wrapper;
        }

        private void OnFrameLoadStart(object sender, FrameLoadStartEventArgs e) {
            _inner.SetZoomLevel(_zoomLevel);
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThreadAsync(() => Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Indetermitate, e.Url)));
            }
        }

        private string OnAcApiRequest(string url) {
            var e = new AcApiRequestEventArgs(url);
            AcApiRequest?.Invoke(this, e);
            return e.Response;
        }

        private void OnRequestHandlerInject(object o, WebInjectEventArgs webInjectEventArgs) {
            Inject?.Invoke(this, webInjectEventArgs);
        }

        private void OnTitleChanged(object o, DependencyPropertyChangedEventArgs args) {
            TitleChanged?.Invoke(this, new Web.TitleChangedEventArgs((string)args.NewValue));
        }

        public event EventHandler<PageLoadingEventArgs> Navigating;
        public event EventHandler<PageLoadedEventArgs> Navigated;
        public event EventHandler<UrlEventArgs> NewWindow;
        public event EventHandler<Web.TitleChangedEventArgs> TitleChanged;

        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThread(() => {
                    Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Ready, e.Url));
                    ModifyPage();
                    Navigated?.Invoke(this, new PageLoadedEventArgs(_inner.Address, null));
                });
            }
        }

        public string GetUrl() {
            return _inner.Address;
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
                AcApiHandler.Register(_inner, _jsBridge?.AcApiHosts.ToArray(), OnAcApiRequest);
                _inner.RegisterJsObject(@"external", _jsBridge, new BindingOptions { CamelCaseJavascriptNames = false });
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
            _downloadHandler.Register(_inner, new[] { @"*" }, listener);
        }

        public void SetNewWindowsBehavior(NewWindowsBehavior mode) {
            _inner.LifeSpanHandler = new LifeSpanHandler(mode, url => { NewWindow?.Invoke(this, new UrlEventArgs(url)); });
        }

        public void ModifyPage() {
            Execute(@"window.__cm_loaded = true;
window.onerror = function(error, url, line, column){ window.external.OnError(error, url, line, column); };");
        }

        public void Execute(string js) {
#if DEBUG
            Logging.Debug(js);
#endif

            try {
                using (var mainFrame = _inner.GetMainFrame()) {
                    mainFrame.ExecuteJavaScriptAsync(js, @"about:contentmanager");
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void Navigate(string url) {
            if (Equals(url, GetUrl())) {
                _inner.Reload(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                return;
            }

            _inner.Address = url;
        }

        public bool CanHandleAcApiRequests => true;
        public event EventHandler<AcApiRequestEventArgs> AcApiRequest;

        public bool IsInjectSupported => true;
        public event EventHandler<WebInjectEventArgs> Inject;

        public bool CanConvertFilenames => true;

        public string ConvertFilename(string filename) {
            return filename == null ? null : new Uri(filename, UriKind.Absolute).AbsoluteUri.Replace(@"file", AltFilesHandlerFactory.SchemeName);
        }

        public ICommand BackCommand => _inner.BackCommand;
        public ICommand ForwardCommand => _inner.ForwardCommand;
        private DelegateCommand<bool?> _refreshCommand;
        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(noCache => { _inner.Reload(noCache == true); }));

        public void OnLoaded() { }
        public void OnUnloaded() { }
        public void OnError(string error, string url, int line, int column) { }

        public Task<string> GetImageUrlAsync(string filename) {
            return Task.FromResult(ConvertFilename(filename));
        }
    }
}