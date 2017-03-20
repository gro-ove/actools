using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.UserControls {
    public partial class WebBlock {
        private readonly IWebSomething _something;

        private static IWebSomething GetSomething() {
            if (PluginsManager.Instance.IsPluginEnabled("CefSharp")) return new CefSharpWrapper();
            if (PluginsManager.Instance.IsPluginEnabled("Awesomium")) return new AwesomiumWrapper();
            return new WebBrowserWrapper();
        }

        public WebBlock() {
            _something = GetSomething();
            _something.Navigating += OnProgressChanged;
            _something.Navigated += OnNavigated;
            var child = _something.Initialize();

            InitializeComponent();
            Children.Add(child);
        }

        private void OnProgressChanged(object sender, PageLoadingEventArgs e) {
            ProgressBar.Visibility = e.Progress.IsReady ? Visibility.Collapsed : Visibility.Visible;
        }

        [ItemCanBeNull]
        public Task<string> GetImageUrlAsync([CanBeNull] string filename) {
            return _something.GetImageUrlAsync(filename);
        }

        public void OnError(string error, string url, int line, int column) {
            _something.OnError(error, url, line, column);
        }

        private void OnNavigated(object sender, PageLoadedEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
            UrlTextBox.Text = e.Url;
            PageLoaded?.Invoke(this, new PageLoadedEventArgs(_something.GetUrl()));

            if (SaveKey != null && e.Url.StartsWith(@"http", StringComparison.OrdinalIgnoreCase)) {
                ValuesStorage.Set(SaveKey, e.Url);
            }
        }

        public void SetScriptProvider(ScriptProviderBase provider) {
            _something.SetScriptProvider(provider);
            provider.Associated = this;
        }

        public void Execute(string js, bool onload = false) {
            _something.Execute(onload ?
                        @"(function(){ var f = function(){" + js + @"}; if (!document.body) window.addEventListener('load', f, false); else f(); })();" :
                        @"(function(){" + js + @"})();");
        }

        public void Execute(string fnName, params object[] args) {
            Execute(fnName, false, args);
        }

        public void Execute(string fnName, bool onload, params object[] args) {
            var js = $"{fnName}({args.Select(JsonConvert.SerializeObject).JoinToString(',')})";
            Execute(js, onload);
        }

        public static readonly DependencyProperty OpenNewWindowsExternallyProperty = DependencyProperty.Register(nameof(OpenNewWindowsExternally), typeof(bool),
                typeof(WebBlock), new PropertyMetadata(true));

        public bool OpenNewWindowsExternally {
            get { return (bool)GetValue(OpenNewWindowsExternallyProperty); }
            set { SetValue(OpenNewWindowsExternallyProperty, value); }
        }

        public static readonly DependencyProperty UserAgentProperty = DependencyProperty.Register(nameof(UserAgent), typeof(string),
                typeof(WebBlock), new PropertyMetadata(OnUserAgentChanged));

        public string UserAgent {
            get { return (string)GetValue(UserAgentProperty); }
            set { SetValue(UserAgentProperty, value); }
        }

        private static void OnUserAgentChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnUserAgentChanged((string)e.NewValue);
        }

        private void OnUserAgentChanged([CanBeNull] string newValue) {
            if (newValue == null) return;
            _something.SetUserAgent(newValue);
        }

        public static readonly DependencyProperty StyleProviderProperty = DependencyProperty.Register(nameof(StyleProvider), typeof(ICustomStyleProvider),
                typeof(WebBlock), new PropertyMetadata(OnStyleProviderChanged));

        [CanBeNull]
        public ICustomStyleProvider StyleProvider {
            get { return (ICustomStyleProvider)GetValue(StyleProviderProperty); }
            set { SetValue(StyleProviderProperty, value); }
        }

        private static void OnStyleProviderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnStyleProviderChanged((ICustomStyleProvider)e.NewValue);
        }

        private void OnStyleProviderChanged(ICustomStyleProvider newValue) {
            _something.SetStyleProvider(newValue);
        }

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(WebBlock));

        [CanBeNull]
        public string SaveKey {
            get { return (string)GetValue(SaveKeyProperty); }
            set { SetValue(SaveKeyProperty, value); }
        }

        public static readonly DependencyProperty StartPageProperty = DependencyProperty.Register(nameof(StartPage), typeof(string),
                typeof(WebBlock), new PropertyMetadata(OnStartPageChanged));

        [CanBeNull]
        public string StartPage {
            get { return (string)GetValue(StartPageProperty); }
            set { SetValue(StartPageProperty, value); }
        }

        private static void OnStartPageChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnStartPageChanged((string)e.NewValue);
        }

        private void OnStartPageChanged([CanBeNull] string newValue) {
            if (_loaded) {
                Navigate(newValue);
            }
        }

        public void Navigate([CanBeNull] string url) {
            _something.Navigate(url ?? @"about:blank");
        }

        public void RefreshPage() {
            _something.RefreshCommand.Execute(null);
        }

        public event EventHandler<PageLoadedEventArgs> PageLoaded;

        private void UrlTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                Navigate(UrlTextBox.Text);
            }
        }

        private void UrlTextBox_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
            }
        }

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _something?.BackCommand.CanExecute(null) == true;
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            _something.BackCommand.Execute(null);
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _something?.ForwardCommand.CanExecute(null) == true;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            _something.ForwardCommand.Execute(null);
        }

        private void GoToPage_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void GoToPage_Executed(object sender, ExecutedRoutedEventArgs a) {
            Navigate(UrlTextBox.Text);
        }

        private void BrowseHome_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void BrowseHome_Executed(object sender, ExecutedRoutedEventArgs e) {
            Navigate(StartPage);
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            _something.OnLoaded();
            Navigate((SaveKey == null ? null : ValuesStorage.GetString(SaveKey)) ?? StartPage);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            _something.OnUnloaded();
        }
    }
}
