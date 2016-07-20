using System;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using Awesomium.Core;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.UserControls {
    internal class AwesomiumWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static AwesomiumWrapper() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent =
                    $"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        private static WebSession _session;

        private BetterWebControl _inner;

        public FrameworkElement Initialize() {
            if (_session == null) {
                if (!WebCore.IsInitialized) {
                    WebCore.Initialize(new WebConfig {
                        UserAgent = DefaultUserAgent,
                        ReduceMemoryUsageOnNavigation = true,
                        LogLevel = LogLevel.None,
                        // RemoteDebuggingHost = "127.0.0.1",
                        // RemoteDebuggingPort = 45451,
                        AdditionalOptions = new [] {
                            @"disable-desktop-notifications"
                        },
                        CustomCSS = @"
::-webkit-scrollbar {
    width: 8px !important;
}

::-webkit-scrollbar-track {
    box-shadow: none !important;
    border-radius: 0 !important;
    background: #000 !important;
}

::-webkit-scrollbar-thumb {
    border: none !important;
    box-shadow: none !important;
    border-radius: 0 !important;
    background: #333 !important;
}

::-webkit-scrollbar-thumb:hover {
    background: #444 !important;
}

::-webkit-scrollbar-thumb:active {
    background: #666 !important;
}"
                    });
                }

                _session = WebCore.CreateWebSession(FilesStorage.Instance.GetTemporaryFilename(@"Awesomium"), new WebPreferences {
                    EnableGPUAcceleration = true,
                    WebGL = true,
                    SmoothScrolling = false
                });
            }

            _inner = new BetterWebControl {
                WebSession = _session,
                UserAgent = DefaultUserAgent
            };
            
            _inner.DocumentReady += OnDocumentReady;
            return _inner;
        }

        private void OnDocumentReady(object sender, DocumentReadyEventArgs e) {
            if (e.ReadyState == DocumentReadyState.Ready && _inner.IsDocumentReady) {
                if (_inner.ExecuteJavascriptWithResult(@"window.__cm_loaded").IsBoolean) return;
                Navigated?.Invoke(this, new PageLoadedEventArgs(_inner.Source.OriginalString));
            }
        }

        public event EventHandler<PageLoadedEventArgs> Navigated;

        public string GetUrl() {
            return _inner.Source.OriginalString;
        }

        public void SetScriptProvider(BaseScriptProvider provider) {
            _inner.ObjectForScripting = provider;
        }

        public void SetUserAgent(string userAgent) {
            WebConfig.Default.UserAgent = userAgent;
            _inner.UserAgent = userAgent;
        }

        public void ModifyPage() {
            Execute(@"window.__cm_loaded = true;
window.onerror = function(err, url, lineNumber){ window.external.Log('error: `' + err + '` script: `' + url + '` line: ' + lineNumber); };
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
            try {
                _inner.ExecuteJavascript(js);
            } catch (Exception e) {
                Logging.Warning("[WebBrowserBlock] Execute(): " + e);
            }
        }

        public void Navigate(string url) {
            try {
                _inner.Source = new Uri(url, UriKind.RelativeOrAbsolute);
            } catch (Exception e) {
                if (!url.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase)) {
                    url = @"http://" + url;
                    try {
                        _inner.Source = new Uri(url, UriKind.RelativeOrAbsolute);
                    } catch (Exception ex) {
                        Logging.Write("[WebBrowserBlock] Navigation failed: " + ex);
                    }
                } else {
                    Logging.Write("[WebBrowserBlock] Navigation failed: " + e);
                }
            }
        }

        public void GoBack() {
            _inner.GoBack();
        }

        public bool CanGoBack() {
            return _inner.CanGoBack();
        }

        public void GoForward() {
            _inner.GoForward();
        }

        public bool CanGoForward() {
            return _inner.CanGoForward();
        }

        public void OnLoaded() {}

        public void OnUnloaded() {}
    }
}