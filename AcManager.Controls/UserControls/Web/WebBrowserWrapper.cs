using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcTools;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    internal class WebBrowserWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static WebBrowserWrapper() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent = $"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        private WebBrowser _inner;

        [CanBeNull]
        private ICustomStyleProvider _styleProvider;

        public FrameworkElement GetElement() {
            if (_inner != null) return _inner;
            WebBrowserHelper.SetUserAgent(DefaultUserAgent);

            _inner = new WebBrowser();
            _inner.Navigated += OnNavigated;
            _inner.LoadCompleted += OnLoadCompleted;
            _inner.Navigating += OnNavigating;
            return _inner;
        }

        private NewWindowsBehavior _newWindowsBehavior;

        public void SetNewWindowsBehavior(NewWindowsBehavior mode) {
            _newWindowsBehavior = mode;
        }

        private SHDocVw.WebBrowser _shBrowser;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AsWebBrowser2() {
            try {
                var serviceProvider = (IServiceProvider)_inner.Document;
                if (serviceProvider != null) {
                    var serviceGuid = new Guid("0002DF05-0000-0000-C000-000000000046");
                    var iid = typeof(SHDocVw.WebBrowser).GUID;
                    _shBrowser = (SHDocVw.WebBrowser)serviceProvider.QueryService(ref serviceGuid, ref iid);
                    if (_shBrowser != null) {
                        _shBrowser.Silent = true;
                        _shBrowser.AddressBar = true;
                        if (_newWindowsBehavior != NewWindowsBehavior.OpenInBrowser) {
                            _shBrowser.NewWindow2 += OnNewWindow2;
                        }
                    } else {
                        Logging.Warning("Pointer=null");
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        // ReSharper disable RedundantAssignment
        private void OnNewWindow2(ref object ppDisp, ref bool cancel) {
            switch (_newWindowsBehavior) {
                case NewWindowsBehavior.Ignore:
                    ppDisp = null;
                    break;
                case NewWindowsBehavior.ReplaceCurrent:
                    ppDisp = typeof(WebBrowser).GetProperty("ActiveXInstance", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty)?
                                               .GetValue(_inner);
                    break;
                case NewWindowsBehavior.OpenInBrowser:
                    // Shouldn’t happen here
                    break;
                case NewWindowsBehavior.MultiTab:
                    // I have no idea how to do that
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            cancel = ppDisp == null;
        }
        // ReSharper restore RedundantAssignment

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
        internal interface IServiceProvider {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object QueryService(ref Guid guidService, ref Guid riid);
        }

        public event EventHandler<PageLoadingEventArgs> Navigating;
        public event EventHandler<UrlEventArgs> Navigated;
        public event EventHandler<UrlEventArgs> NewWindow;
        public event EventHandler<TitleChangedEventArgs> TitleChanged;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e) {
            Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Indetermitate, e.Uri.ToString()));
        }

        private void OnLoadCompleted(object sender, NavigationEventArgs e) {
            Navigating?.Invoke(this, new PageLoadingEventArgs(AsyncProgressEntry.Ready, e.Uri.ToString()));
        }

        private void OnNavigated(object sender, NavigationEventArgs e) {
            AsWebBrowser2();
            ModifyPage();

            var userCss = _styleProvider?.ToScript(e.Uri.OriginalString);
            if (userCss != null) {
                Execute(userCss);
            }

            Navigated?.Invoke(this, new UrlEventArgs(e.Uri.OriginalString));

            try {
                TitleChanged?.Invoke(this, new TitleChangedEventArgs((string)((dynamic)_inner.Document).Title));
            } catch (Exception ex) {
                Logging.Error(ex);
            }
        }

        public void ModifyPage() {
            Execute(@"window.__cm_loaded = true;
window.onerror = function(error, url, line, column){ window.external.OnError(error, url, line, column); };");
        }

        public string GetUrl() {
            return _inner.Source?.OriginalString ?? "";
        }

        public void SetScriptProvider(ScriptProviderBase provider) {
            _inner.ObjectForScripting = provider;
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
            if (_inner.Source == null) return null;
            try {
                _errorHappened = false;
                return _inner.InvokeScript(@"eval", js);
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
                _inner.Refresh(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                return;
            }

            try {
                _inner.Navigate(url);
            } catch (Exception e) {
                if (!url.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase)) {
                    url = @"http://" + url;
                    try {
                        _inner.Navigate(url);
                    } catch (Exception ex) {
                        Logging.Write("Navigation failed: " + ex);
                    }
                } else {
                    Logging.Write("Navigation failed: " + e);
                }
            }
        }

        private DelegateCommand _goBackCommand;

        public ICommand BackCommand => _goBackCommand ?? (_goBackCommand = new DelegateCommand(() => { _inner.GoBack(); }, () => _inner.CanGoBack));

        private DelegateCommand _goForwardCommand;

        public ICommand ForwardCommand
            => _goForwardCommand ?? (_goForwardCommand = new DelegateCommand(() => { _inner.GoForward(); }, () => _inner.CanGoForward));

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(noCache => { _inner.Refresh(noCache == true); }));

        private DispatcherTimer _timer;

        public void OnLoaded() {
            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(0.5),
                IsEnabled = true
            };

            _timer.Tick += OnTick;
        }

        public void OnUnloaded() {
            _timer.IsEnabled = false;
            _timer = null;
        }

        private void OnTick(object sender, EventArgs e) {
            // Logging.Debug(Execute(@"!window.__cm_loaded"));
            // Execute(@"if (!window.__cm_loaded){ window.external.FixPage(); }");
        }

        public async Task<string> GetImageUrlAsync(string filename) {
            return File.Exists(filename) ? $@"data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(filename))}" : null;
        }
    }
}