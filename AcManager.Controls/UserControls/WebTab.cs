using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.UserControls.CefSharp;
using AcManager.Controls.UserControls.Web;
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

namespace AcManager.Controls.UserControls {
    public class WebTab : NotifyPropertyChanged {
        [NotNull]
        private static IWebSomething GetSomething() {
            if (PluginsManager.Instance.IsPluginEnabled(KnownPlugins.CefSharp)) return new CefSharpWrapper();
            // return new WebBrowserWrapper();
            return new FormsWebBrowserWrapper();
        }

        private readonly IWebSomething _something;
        private readonly bool _preferTransparentBackground;
        private string _delayedUrl;

        public WebTab(string url, bool preferTransparentBackground, bool delayed) {
            _preferTransparentBackground = preferTransparentBackground;
            _something = GetSomething();
            _something.Navigating += OnProgressChanged;
            _something.Navigated += OnNavigated;
            _something.NewWindow += OnNewWindow;
            _something.TitleChanged += OnTitleChanged;
            _something.Inject += OnInject;
            _something.AcApiRequest += OnAcApiRequest;
            _something.OnLoaded();

            // We need to initialize element first to be able to use Navigate().
            // TODO: Find a better way without overcomplicating everything?
            _something.GetElement(null, preferTransparentBackground);

            Title = url;
            LoadedUrl = ActiveUrl = url;

            if (delayed) {
                _delayedUrl = url;
            } else {
                Navigate(url);
            }
        }

        public void EnsureLoaded() {
            var delayedUrl = _delayedUrl;
            if (delayedUrl == null) return;
            _delayedUrl = null;
            Navigate(delayedUrl);
        }

        public FrameworkElement GetElement([CanBeNull] DpiAwareWindow parentWindow) {
            return _something.GetElement(parentWindow, _preferTransparentBackground);
        }

        private string _title;

        public string Title {
            get => _title;
            private set => Apply(value, ref _title);
        }

        private void OnTitleChanged(object sender, TitleChangedEventArgs e) {
            Title = e.Title;
        }

        public ICommand BackCommand => _something.BackCommand;
        public ICommand ForwardCommand => _something.ForwardCommand;
        public ICommand RefreshCommand => _something.RefreshCommand;

        public void Navigate([CanBeNull] string url) {
            url = url ?? @"about:blank";
            if (_delayedUrl != null) {
                _delayedUrl = url;
            } else {
                _something.Navigate(url);
            }
        }

        [ContractAnnotation(@"filename: null => null; filename: notnull => notnull")]
        public string ConvertFilename([CanBeNull] string filename) {
            return _something.ConvertFilename(filename);
        }

        [ItemCanBeNull]
        public Task<string> GetImageUrlAsync([CanBeNull] string filename) {
            return _something.GetImageUrlAsync(filename);
        }

        public void OnError(string error, string url, int line, int column) {
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

        public void Execute(string js, bool onload = false) {
            try {
                _something.Execute(onload ?
                        @"(function(){ var f = function(){" + js + @"}; if (!document.body) window.addEventListener('load', f, false); else f(); })();" :
                        @"(function(){" + js + @"})();");
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void Execute(string fnName, params object[] args) {
            Execute(fnName, false, args);
        }

        public void Execute(string fnName, bool onload, params object[] args) {
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
            _jsBridge?.PageInject(e.Url, e.ToInject);
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

        public event EventHandler<UrlEventArgs> PageLoading;

        private void OnProgressChanged(object sender, PageLoadingEventArgs e) {
            ActiveUrl = e.Url;
            IsLoading = !e.Progress.IsReady;
            PageLoading?.Invoke(this, new UrlEventArgs(e.Url ?? ""));
        }

        [CanBeNull]
        private JsBridgeBase _jsBridge;

        public T GetJsBridge<T>([CanBeNull] Func<WebTab, T> jsBridgeFactory) where T : JsBridgeBase {
            var result = _something.GetJsBridge(() => jsBridgeFactory?.Invoke(this));
            _jsBridge = result;
            return result;
        }

        public void SetUserAgent(string userAgent) {
            _something.SetUserAgent(userAgent);
        }

        public void SetStyleProvider(ICustomStyleProvider styleProvider) {
            _something.SetStyleProvider(styleProvider);
        }

        public void SetDownloadListener(IWebDownloadListener downloadListener) {
            _something.SetDownloadListener(downloadListener);
        }

        public void SetNewWindowsBehavior(NewWindowsBehavior newWindowsBehavior) {
            _something.SetNewWindowsBehavior(newWindowsBehavior);
        }

        public void OnUnloaded() {
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

        public DelegateCommand CloseCommand => _closeCommand ?? (_closeCommand = new DelegateCommand(() => {
            IsClosed = true;
        }, () => !IsMainTab));
    }
}