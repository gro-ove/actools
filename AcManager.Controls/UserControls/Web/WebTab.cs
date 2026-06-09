using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.UserControls.Cef;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.UserControls.Web {
    public class WebTab : NotifyPropertyChanged {
        private static bool? _cefSharpMode;

        [NotNull]
        private static IWebSomething GetSomething() {
            return PluginsManager.Instance.IsPluginEnabled(KnownPlugins.CefSharp)
                    ? GetCefSomething()
                    : new FormsWebBrowserWrapper();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IWebSomething GetCefSomething() {
            if (_cefSharpMode ?? (_cefSharpMode = SettingsHolder.Plugins.CefWinForms).Value) {
                return new FormsCefSharpWrapper();
            }

            return new CefSharpWrapper();
        }

        private readonly IWebSomething _something;
        private readonly bool _preferTransparentBackground;
        private string _delayedUrl;

        private readonly bool _broken;
        private readonly Exception _exception;

        public WebTab(string url, bool preferTransparentBackground, bool delayed) {
            _preferTransparentBackground = preferTransparentBackground;
            _something = GetSomething();
            _something.LoadingStateChanged += OnLoadingStateChanged;
            _something.PageLoadingStarted += OnPageLoadingStarted;
            _something.PageLoaded += OnNavigated;
            _something.NewWindow += OnNewWindow;
            _something.TitleChanged += OnTitleChanged;
            _something.AddressChanged += OnAddressChanged;
            _something.FaviconChanged += OnFaviconChanged;
            _something.Inject += OnInject;
            _something.Headers += OnHeaders;
            _something.AcApiRequest += OnAcApiRequest;
            _something.OnLoaded();

            // We need to initialize element first to be able to use Navigate().
            // TODO: Find a better way without overcomplicating everything?
            try {
                _something.GetElement(null, preferTransparentBackground);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t initialize browser engine", e);
                _broken = true;
                _exception = e;
                return;
            }

            Title = url;
            LoadedUrl = ActiveUrl = url;

            if (delayed) {
                _delayedUrl = url;
            } else {
                Navigate(url);
            }
        }

        private void OnAddressChanged(object sender, UrlEventArgs e) {
            ActiveUrl = e.Url;
        }

        public void EnsureLoaded() {
            if (_broken) return;
            var delayedUrl = _delayedUrl;
            if (delayedUrl == null) return;
            _delayedUrl = null;
            Navigate(delayedUrl);
        }

        [CanBeNull]
        public FrameworkElement GetElement([CanBeNull] DpiAwareWindow parentWindow) {
            if (_broken) {
                return new ExceptionDetails {
                    Title = "Failed to initialize browser engine",
                    Exception = _exception,
                    Suggestions = GetBrokenSuggestions()
                };
            }

            return _something.GetElement(parentWindow, _preferTransparentBackground);
        }

        private string GetBrokenSuggestions() {
            if (_something is CefSharpWrapper || _something is FormsCefSharpWrapper) {
                var directory = @"file://" + PluginsManager.Instance.GetPluginDirectory(KnownPlugins.CefSharp);
                return
                        $"Try to reinstall CefSharp plugin, its installation could be damaged. To do that, [url={BbCodeBlock.EncodeAttribute(directory)}]remove this directory[/url] (you’ll need to close the app to be able to remove it).";
            }

            return null;
        }

        private string _title;

        public string Title {
            get => _title;
            private set => Apply(value, ref _title);
        }

        private void OnTitleChanged(object sender, TitleChangedEventArgs e) {
            Title = e.Title;
        }

        public ICommand BackCommand => _broken ? UnavailableCommand.Instance : _something.BackCommand;
        public ICommand ForwardCommand => _broken ? UnavailableCommand.Instance : _something.ForwardCommand;
        public ICommand RefreshCommand => _broken ? UnavailableCommand.Instance : _something.RefreshCommand;

        public void Navigate([CanBeNull] string url) {
            if (_broken) return;
            url = url ?? @"about:blank";
            if (_delayedUrl != null) {
                _delayedUrl = url;
            } else {
                _something.Navigate(url);
            }
        }

        public void ShowDevTools() {
            _something.ShowDevTools();
        }

        [ContractAnnotation(@"filename: null => null; filename: notnull => notnull")]
        public string ConvertFilename([CanBeNull] string filename) {
            return _broken ? filename : _something.ConvertFilename(filename);
        }

        [ItemCanBeNull]
        public Task<string> GetImageUrlAsync([CanBeNull] string filename) {
            return _broken ? Task.FromResult<string>(null) : _something.GetImageUrlAsync(filename);
        }

        public void OnError(string error, string url, int line, int column) {
            if (_broken) return;
            _something.OnError(error, url, line, column);
        }

        private string _loadedUrl;

        [CanBeNull]
        public string LoadedUrl {
            get => _loadedUrl;
            private set => Apply(value, ref _loadedUrl);
        }

        private string _activeUrl;

        [CanBeNull]
        public string ActiveUrl {
            get => _activeUrl;
            private set => Apply(value, ref _activeUrl, UpdateFavicon);
        }

        private void UpdateFavicon() {
            if (_broken) return;
            if (_something.SupportsFavicons) return;
            FaviconProvider.GetFaviconAsync(ActiveUrl).ContinueWith(t => {
                if (t.Result != null) {
                    ActionExtension.InvokeInMainThreadAsync(() => Favicon = t.Result);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private string _favicon;

        [CanBeNull]
        public string Favicon {
            get => _favicon;
            private set => Apply(value, ref _favicon);
        }

        private void OnFaviconChanged(object sender, FaviconChangedEventArgs e) {
            Favicon = e.Url;
        }

        public void Execute(string js, bool onload = false) {
            if (_broken) return;

            ActionExtension.InvokeInMainThreadAsync(() => {
                try {
                    _something.Execute(onload ?
                            @"(function(){ var f = function(){" + js + @"}; if (!document.body) window.addEventListener('load', f, false); else f(); })();" :
                            @"(function(){" + js + @"})();");
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            });
        }

        public void Execute(string fnName, params object[] args) {
            if (_broken) return;
            Execute(fnName, false, args);
        }

        public void Execute(string fnName, bool onload, params object[] args) {
            if (_broken) return;
            var js = $"{fnName}({args.Select(JsonConvert.SerializeObject).JoinToString(',')})";
            Execute(js, onload);
        }

        private void OnNavigated(object sender, UrlEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
            LoadedUrl = ActiveUrl = e.Url;
            _jsBridge?.PageLoaded(e.Url);
            PageLoaded?.Invoke(this, new UrlEventArgs(e.Url));
        }

        private void OnInject(object sender, WebInjectEventArgs e) {
            _jsBridge?.PageInject(e.Url, e.ToInject, e.Replacements);
        }

        private void OnHeaders(object sender, WebHeadersEventArgs e) {
            _jsBridge?.PageHeaders(e.Url, e.Headers);
        }

        private void OnAcApiRequest(object sender, AcApiRequestEventArgs e) {
            e.Response = _jsBridge?.AcApiRequest(e.RequestUrl);
        }

        public event EventHandler<UrlEventArgs> PageLoaded;

        private void OnNewWindow(object sender, NewWindowEventArgs e) {
            NewWindow?.Invoke(this, e);
        }

        public event EventHandler<NewWindowEventArgs> NewWindow;

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            private set => Apply(value, ref _isLoading);
        }

        private void OnLoadingStateChanged(object sender, PageLoadingEventArgs e) {
            IsLoading = !e.Progress.IsReady;
            CommandManager.InvalidateRequerySuggested();
        }

        public event EventHandler<UrlEventArgs> PageLoadingStarted;

        private void OnPageLoadingStarted(object sender, UrlEventArgs e) {
            ActiveUrl = e.Url;
            PageLoadingStarted?.Invoke(this, e);
        }

        [CanBeNull]
        private JsBridgeBase _jsBridge;

        [CanBeNull]
        public T GetJsBridge<T>([CanBeNull] Func<WebTab, T> jsBridgeFactory) where T : JsBridgeBase {
            if (_broken) return null;
            var result = _something.GetJsBridge(() => jsBridgeFactory?.Invoke(this));
            _jsBridge = result;
            return result;
        }

        public void SetUserAgent(string userAgent) {
            if (_broken) return;
            _something.SetUserAgent(userAgent);
        }

        public void SetStyleProvider(ICustomStyleProvider styleProvider) {
            if (_broken) return;
            _something.SetStyleProvider(styleProvider);
        }

        public void SetDownloadListener(IWebDownloadListener downloadListener) {
            if (_broken) return;
            _something.SetDownloadListener(downloadListener);
        }

        public void SetNewWindowsBehavior(NewWindowsBehavior newWindowsBehavior) {
            if (_broken) return;
            _something.SetNewWindowsBehavior(newWindowsBehavior);
        }

        public void OnUnloaded() {
            if (_broken) return;
            _something.OnUnloaded();
        }

        private bool _isClosed;

        public bool IsClosed {
            get => _isClosed;
            set => Apply(value, ref _isClosed);
        }

        private bool _isMainTab;

        public bool IsMainTab {
            get => _isMainTab;
            set => Apply(value, ref _isMainTab, () => _closeCommand?.RaiseCanExecuteChanged());
        }

        private DelegateCommand _closeCommand;

        public DelegateCommand CloseCommand => _closeCommand ?? (_closeCommand = new DelegateCommand(() => IsClosed = true, () => !IsMainTab));
    }
}