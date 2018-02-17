using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
using LogSeverity = CefSharp.LogSeverity;

namespace AcManager.Controls.UserControls.CefSharp {
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

        public FrameworkElement GetElement() {
            if (_inner != null) return _inner;

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
                    WebSecurity = CefState.Disabled,
                    OffScreenTransparentBackground = false,
                },
                RequestHandler = _requestHandler,
                MenuHandler = new MenuHandler(),
                DownloadHandler = new DownloadHandler(),
            };

            _inner.FrameLoadStart += OnFrameLoadStart;
            _inner.FrameLoadEnd += OnFrameLoadEnd;
            _inner.TitleChanged += OnTitleChanged;
            return _inner;
        }

        private void OnTitleChanged(object o, DependencyPropertyChangedEventArgs args) {
            TitleChanged?.Invoke(this, new Web.TitleChangedEventArgs((string)args.NewValue));
        }

        public event EventHandler<PageLoadingEventArgs> Navigating;
        public event EventHandler<UrlEventArgs> Navigated;
        public event EventHandler<UrlEventArgs> NewWindow;
        public event EventHandler<Web.TitleChangedEventArgs> TitleChanged;

        private void OnFrameLoadStart(object sender, FrameLoadStartEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThreadAsync(() => Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Indetermitate, e.Url)));
            }
        }

        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e) {
            if (e.Frame.IsMain) {
                ActionExtension.InvokeInMainThread(() => {
                    Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Ready, e.Url));
                    ModifyPage();
                    Navigated?.Invoke(this, new UrlEventArgs(_inner.Address));
                });
            }
        }

        public string GetUrl() {
            return _inner.Address;
        }

        public void SetJsBridge(JsBridgeBase bridge) {
            try {
                _inner.RegisterJsObject(@"external", bridge, new BindingOptions { CamelCaseJavascriptNames = false });
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void SetUserAgent(string userAgent) {
            _requestHandler.UserAgent = userAgent;
        }

        public void SetStyleProvider(ICustomStyleProvider provider) {
            _requestHandler.StyleProvider = provider;
        }

        public void SetNewWindowsBehavior(NewWindowsBehavior mode) {
            _inner.LifeSpanHandler = new LifeSpanHandler(mode, url => {
                NewWindow?.Invoke(this, new UrlEventArgs(url));
            });
        }

        public void ModifyPage() {
            Execute(@"window.__cm_loaded = true;
window.onerror = function(error, url, line, column){ window.external.OnError(error, url, line, column); };");
        }

        public void Execute(string js) {
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

        public ICommand BackCommand => _inner.BackCommand;

        public ICommand ForwardCommand => _inner.ForwardCommand;

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(noCache => { _inner.Reload(noCache == true); }));

        public void OnLoaded() { }

        public void OnUnloaded() { }

        public void OnError(string error, string url, int line, int column) { }

        public Task<string> GetImageUrlAsync(string filename) {
            return Task.FromResult(filename == null ? null : new Uri(filename, UriKind.Absolute).AbsoluteUri.Replace(@"file", AltFilesHandlerFactory.SchemeName));
        }
    }
}