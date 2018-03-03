using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    internal class FormsWebBrowserWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static FormsWebBrowserWrapper() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent = $"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        [CanBeNull]
        private WebBrowser _inner;

        [CanBeNull]
        private WindowsFormsHost _wrapper;

        [CanBeNull]
        private ICustomStyleProvider _styleProvider;

        public FrameworkElement GetElement(DpiAwareWindow parentWindow, bool preferTransparentBackground) {
            if (_wrapper != null) return _wrapper;
            WebBrowserHelper.SetUserAgent(DefaultUserAgent);

            _inner = new WebBrowser { ScriptErrorsSuppressed = true };
            _inner.Navigated += OnNavigated;
            _inner.Navigating += OnNavigating;
            _inner.NewWindow += OnNewWindow;
            _inner.DocumentTitleChanged += OnTitleChanged;
            _wrapper = new WindowsFormsHost {
                Child = _inner
            };

            return _wrapper;
        }

        private void OnNewWindow(object sender, CancelEventArgs args) {
            switch (_newWindowsBehavior) {
                case NewWindowsBehavior.Ignore:
                    args.Cancel = true;
                    break;
                case NewWindowsBehavior.ReplaceCurrent:
                    _inner?.Navigate(_inner.StatusText);
                    args.Cancel = true;
                    break;
                case NewWindowsBehavior.OpenInBrowser:
                    break;
                case NewWindowsBehavior.MultiTab:
                    NewWindow?.Invoke(this, new NewWindowEventArgs(_inner?.StatusText ?? string.Empty));
                    args.Cancel = true;
                    break;
                case NewWindowsBehavior.Callback:
                    var newArgs = new NewWindowEventArgs(_inner?.StatusText ?? string.Empty);
                    NewWindow?.Invoke(this, newArgs);
                    args.Cancel = newArgs.Cancel;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnTitleChanged(object sender, EventArgs e) {
            TitleChanged?.Invoke(this, new TitleChangedEventArgs(_inner?.DocumentTitle ?? _inner?.Url.ToString() ?? string.Empty));
        }

        public void SetDownloadListener(IWebDownloadListener listener) {
            // Not supported
        }

        private NewWindowsBehavior _newWindowsBehavior;

        public void SetNewWindowsBehavior(NewWindowsBehavior mode) {
            _newWindowsBehavior = mode;
        }


        public event EventHandler<UrlEventArgs> PageLoadingStarted;
        public event EventHandler<UrlEventArgs> PageLoaded;
        public event EventHandler<PageLoadingEventArgs> LoadingStateChanged;
        public event EventHandler<NewWindowEventArgs> NewWindow;
        public event EventHandler<UrlEventArgs> AddressChanged;
        public event EventHandler<TitleChangedEventArgs> TitleChanged;
        public event EventHandler<FaviconChangedEventArgs> FaviconChanged;
        public bool SupportsFavicons => false;

        public bool CanHandleAcApiRequests => false;
        public event EventHandler<AcApiRequestEventArgs> AcApiRequest;

        public bool IsInjectSupported => false;
        public event EventHandler<WebInjectEventArgs> Inject;

        public bool CanConvertFilenames => true;

        public string ConvertFilename(string filename) {
            return filename == null ? null : new Uri(filename, UriKind.Absolute).AbsoluteUri;
        }

        private void OnNavigating(object sender, WebBrowserNavigatingEventArgs args) {
            if (_inner == null || _inner.Url != args.Url) return;
            PageLoadingStarted?.Invoke(this, new UrlEventArgs(args.Url.ToString()));
            LoadingStateChanged?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Indetermitate, args.Url.ToString()));
        }

        /*private void OnLoadCompleted(object sender, NavigationEventArgs e) {
            Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Ready, e.Uri.ToString()));
        }*/

        private void OnNavigated(object sender, WebBrowserNavigatedEventArgs args) {
            if (_inner == null || _inner.Url != args.Url) return;

            var userCss = _styleProvider?.ToScript(args.Url.OriginalString, false);
            if (userCss != null) {
                Execute(userCss);
            }

            PageLoaded?.Invoke(this, new UrlEventArgs(args.Url.ToString()));
            LoadingStateChanged?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Ready, args.Url.ToString()));
        }

        public string GetUrl() {
            return _inner?.Url?.OriginalString ?? "";
        }

        private bool _jsBridgeSet;

        [CanBeNull]
        private JsBridgeBase _jsBridge;

        public T GetJsBridge<T>(Func<T> factory) where T : JsBridgeBase {
            if (_jsBridgeSet) return (T)_jsBridge;

            _jsBridge = factory();
            _jsBridgeSet = true;

            try {
                if (_inner != null) {
                    _inner.ObjectForScripting = _jsBridge;
                }
            } catch (ArgumentException) {
                Logging.Warning("Failed to set: " + (_jsBridge?.GetType().FullName ?? @"NULL"));
                throw;
            }

            return (T)_jsBridge;
        }

        public void SetUserAgent(string userAgent) {
            WebBrowserHelper.SetUserAgent(userAgent);
        }

        public void SetStyleProvider(ICustomStyleProvider provider) {
            _styleProvider = provider;
        }

        private bool _errorHappened;

        public void OnError(string error, string url, int line, int column) {
            _errorHappened = true;
        }

        public object Execute(string js) {
            try {
                _errorHappened = false;
                return _inner?.Document?.InvokeScript(@"eval", new object[] { js });
            } catch (InvalidOperationException e) {
                Logging.Warning("InvalidOperationException: " + e.Message);
            } catch (COMException e) {
                Logging.Warning("COMException: " + e.Message);
            } catch (Exception e) {
                Logging.Warning(e);
            }

            if (_errorHappened) {
                Logging.Debug("error happened while invoking: " + js);
            }

            _errorHappened = false;
            return null;
        }

        void IWebSomething.Execute(string js) {
            Execute(js);
        }

        public void Navigate(string url) {
            if (Equals(url, GetUrl())) {
                _inner?.Refresh(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                        ? WebBrowserRefreshOption.Completely
                        : WebBrowserRefreshOption.Normal);
                return;
            }

            try {
                _inner?.Navigate(url);
            } catch (Exception e) {
                if (url.IsWebUrl()) {
                    Logging.Write("Navigation failed: " + e);
                } else {
                    url = @"http://" + url;
                    try {
                        _inner?.Navigate(url);
                    } catch (Exception ex) {
                        Logging.Write("Navigation failed: " + ex);
                    }
                }
            }
        }

        private DelegateCommand _goBackCommand;

        public ICommand BackCommand => _goBackCommand
                ?? (_goBackCommand = new DelegateCommand(() => _inner?.GoBack(), () => _inner?.CanGoBack == true));

        private DelegateCommand _goForwardCommand;

        public ICommand ForwardCommand => _goForwardCommand
                ?? (_goForwardCommand = new DelegateCommand(() => _inner?.GoForward(), () => _inner?.CanGoForward == true));

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(
                noCache => _inner?.Refresh(noCache == true ? WebBrowserRefreshOption.Completely : WebBrowserRefreshOption.Normal)));

        public void OnLoaded() {}

        public void OnUnloaded() {
            DisposeHelper.Dispose(ref _inner);
            DisposeHelper.Dispose(ref _wrapper);
        }

        public async Task<string> GetImageUrlAsync(string filename) {
            return File.Exists(filename)
                    ? $@"data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(filename).ConfigureAwait(false))}" : null;
        }
    }
}