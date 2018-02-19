using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.UserControls.CefSharp;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
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
            return new WebBrowserWrapper();
        }

        private readonly IWebSomething _something;
        private readonly bool _preferTransparentBackground;

        public WebTab(string url, bool preferTransparentBackground) {
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
            Navigate(url);
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
            _something.Navigate(url ?? @"about:blank");
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

        public string LoadedUrl {
            get => _loadedUrl;
            private set => Apply(value, ref _loadedUrl);
        }

        private string _activeUrl;

        public string ActiveUrl {
            get => _activeUrl;
            private set => Apply(value, ref _activeUrl, () => {
                // Favicon = Regex.Replace(value, @"(?<=\w)/.+", "") + @"/favicon.ico";
                Favicon = $@"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(value)}";
            });
        }

        private string _favicon;

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

        private void OnNewWindow(object sender, UrlEventArgs e) {
            NewWindow?.Invoke(this, e);
        }

        public event EventHandler<UrlEventArgs> NewWindow;

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            private set => Apply(value, ref _isLoading);
        }

        private void OnProgressChanged(object sender, PageLoadingEventArgs e) {
            ActiveUrl = e.Url;
            IsLoading = !e.Progress.IsReady;
        }

        [CanBeNull]
        private JsBridgeBase _jsBridge;

        public void SetJsBridge(Func<WebTab, JsBridgeBase> jsBridgeFactory) {
            _something.SetJsBridge(_jsBridge = jsBridgeFactory(this));
        }

        public void SetUserAgent(string userAgent) {
            _something.SetUserAgent(userAgent);
        }

        public void SetStyleProvider(ICustomStyleProvider styleProvider) {
            _something.SetStyleProvider(styleProvider);
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