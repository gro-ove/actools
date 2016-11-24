using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using Awesomium.Core;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    internal class AwesomiumWrapper : IWebSomething {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static AwesomiumWrapper() {
            var windows = $@"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            DefaultUserAgent =
                    $@"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        private static WebSession _session;

        private BetterWebControl _inner;

        [CanBeNull]
        private ICustomStyleProvider _styleProvider;

        public FrameworkElement Initialize() {
            if (_session == null) {
                if (!WebCore.IsInitialized) {
                    WebCore.Initialize(new WebConfig {
                        UserAgent = DefaultUserAgent,
                        ReduceMemoryUsageOnNavigation = true,
                        LogLevel = LogLevel.None,
#if DEBUG
                        RemoteDebuggingHost = @"127.0.0.1",
                        RemoteDebuggingPort = 45451,
#endif
                        AdditionalOptions = new[] {
                            @"disable-desktop-notifications"
                        },
                        CustomCSS = @"
::-webkit-scrollbar { width: 8px!important; height: 8px!important; }
::-webkit-scrollbar-track { box-shadow: none!important; border-radius: 0!important; background: #000!important; }
::-webkit-scrollbar-corner { background: #000 !important; }
::-webkit-scrollbar-thumb { border: none !important; box-shadow: none !important; border-radius: 0 !important; background: #333 !important; }
::-webkit-scrollbar-thumb:hover { background: #444 !important; }
::-webkit-scrollbar-thumb:active { background: #666 !important; }"
                    });
                }

                _session = WebCore.CreateWebSession(FilesStorage.Instance.GetTemporaryFilename(@"Awesomium"), new WebPreferences {
                    EnableGPUAcceleration = true,
                    WebGL = true,
                    SmoothScrolling = false,
                    FileAccessFromFileURL = true,
                    UniversalAccessFromFileURL = true
                });
            }

            _inner = new BetterWebControl {
                WebSession = _session,
                UserAgent = DefaultUserAgent
            };

            _inner.LoadingFrame += OnLoadingFrame;
            _inner.LoadingFrameComplete += OnLoadingFrameComplete;
            _inner.LoadingFrameFailed += OnLoadingFrameComplete;
            _inner.DocumentReady += OnDocumentReady;
            return _inner;
        }

        public event EventHandler<PageLoadingEventArgs> Navigating;

        public event EventHandler<PageLoadedEventArgs> Navigated;

        private void OnLoadingFrame(object sender, LoadingFrameEventArgs e) {
            Navigating?.Invoke(this, PageLoadingEventArgs.Indetermitate);
        }

        private void OnLoadingFrameComplete(object sender, FrameEventArgs e) {
            Navigating?.Invoke(this, PageLoadingEventArgs.Ready);
        }

        private void OnDocumentReady(object sender, DocumentReadyEventArgs e) {
            if (e.ReadyState == DocumentReadyState.Ready && _inner.IsDocumentReady) {
                if (_inner.ExecuteJavascriptWithResult(@"window.__cm_loaded").IsBoolean) return;

                ModifyPage();

                var userCss = _styleProvider?.ToScript(_inner.Source.OriginalString);
                if (userCss != null) {
                    Execute(userCss);
                }

                Navigated?.Invoke(this, new PageLoadedEventArgs(_inner.Source.OriginalString));
            }
        }

        public string GetUrl() {
            return _inner.Source.OriginalString;
        }

        public void SetScriptProvider(ScriptProviderBase provider) {
            _inner.ObjectForScripting = provider;
        }

        public void SetUserAgent(string userAgent) {
            WebConfig.Default.UserAgent = userAgent;
            _inner.UserAgent = userAgent;
        }

        public void SetStyleProvider(ICustomStyleProvider provider) {
            _styleProvider = provider;
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
            try {
                js = $@"(function(){{ try {{ {js} }} catch(e){{ window.external.OnError(e ? '' + (e.stack || e) : '?', '<execute>', -1, -1); }} }})()";
                _inner.ExecuteJavascript(js);
            } catch (Exception e) {
                Logging.Warning("Execute(): " + e);
            }
        }

        public void Navigate(string url) {
            if (Equals(url, GetUrl())) {
                _inner.Reload(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                return;
            }

            _inner.Navigate(url);
        }

        private DelegateCommand _goBackCommand;

        public ICommand BackCommand => _goBackCommand ?? (_goBackCommand = new DelegateCommand(() => {
            _inner.GoBack();
        }, () => _inner.CanGoBack()));

        private DelegateCommand _goForwardCommand;

        public ICommand ForwardCommand => _goForwardCommand ?? (_goForwardCommand = new DelegateCommand(() => {
            _inner.GoForward();
        }, () => _inner.CanGoForward()));

        private DelegateCommand<bool?> _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand<bool?>(noCache => {
            _inner.Reload(noCache == true);
        }));

        public void OnLoaded() {}

        public void OnUnloaded() {}

        public void OnError(string error, string url, int line, int column) {}

        public async Task<string> GetImageUrlAsync(string filename) {
            return File.Exists(filename) ? $@"data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(filename))}" : null;
        }
    }
}