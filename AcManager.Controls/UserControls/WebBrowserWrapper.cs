using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.UserControls {
    internal class WebBrowserWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static WebBrowserWrapper() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent = $"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        private WebBrowser _inner;

        public FrameworkElement Initialize() {
            WebBrowserHelper.SetUserAgent(DefaultUserAgent);

            _inner = new WebBrowser();
            _inner.Navigated += OnNavigated;
            return _inner;
        }

        private void OnNavigated(object sender, NavigationEventArgs e) {
            WebBrowserHelper.SetSilent(_inner, true);
            Navigated?.Invoke(this, new PageLoadedEventArgs(e.Uri.OriginalString));
        }

        public event EventHandler<PageLoadedEventArgs> Navigated;

        public string GetUrl() {
            return _inner.Source.OriginalString;
        }

        public void SetScriptProvider(ScriptProviderBase provider) {
            _inner.ObjectForScripting = provider;
        }

        public void SetUserAgent(string userAgent) {
            WebBrowserHelper.SetUserAgent(userAgent);
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

        private bool _errorHappened;

        public void OnError(string error, string url, int line, int column) {
            _errorHappened = true;
        }

        public void Execute(string js) {
            if (_inner.Source == null) return;
            try {
                _errorHappened = false;
                _inner.InvokeScript(@"eval", js);
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
        }

        public void Navigate(string url) {
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

        public void GoBack() {
            _inner.GoBack();
        }

        public bool CanGoBack() {
            return _inner.CanGoBack;
        }

        public void GoForward() {
            _inner.GoForward();
        }

        public bool CanGoForward() {
            return _inner.CanGoForward;
        }

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
            Execute(@"if (!window.__cm_loaded){ window.external.FixPage(); }");
        }

        public async Task<string> GetImageUrlAsync(string filename) {
            return File.Exists(filename) ? $@"data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(filename))}" : null;
        }
    }
}