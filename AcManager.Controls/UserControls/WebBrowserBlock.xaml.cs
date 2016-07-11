using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public class PageLoadedEventArgs : EventArgs {
        public PageLoadedEventArgs(string url) {
            Url = url;
        }

        public string Url { get; }
    }

    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [ComVisible(true)]
    public abstract class BaseScriptProvider {
        private WeakReference<WebBrowserBlock> _lastAssociatedWebBrowser;

        [CanBeNull]
        public WebBrowserBlock Associated {
            get {
                WebBrowserBlock res = null;
                return _lastAssociatedWebBrowser?.TryGetTarget(out res) == true ? res : null;
            }
            set { _lastAssociatedWebBrowser = value == null ? null : new WeakReference<WebBrowserBlock>(value); }
        }

        public void NavigateTo(string url) {
            Process.Start(url);
        }

        public void Log(string message) {
            Logging.Write("[ScriptProvider] " + message);
        }

        public void Alert(string message) {
            ModernDialog.ShowMessage(message);
        }

        public string Prompt(string message, string defaultValue) {
            return Dialogs.Prompt.Show(message, "Webpage says", defaultValue);
        }

        public void FixPage() {
            Associated?.ModifyPage();
        }

        public object CmTest() {
            return true;
        }
    }

    public partial class WebBrowserBlock {
        #region Initialization
        public static readonly string DefaultUserAgent;

        static WebBrowserBlock() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? " WOW64;" : "")}";
            DefaultUserAgent = $"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        public WebBrowserBlock() {
            InitializeComponent();
            WebBrowserHelper.SetUserAgent(DefaultUserAgent);
        }

        public WebBrowser Inner => WebBrowser;

        public void SetScriptProvider(BaseScriptProvider provider) {
            WebBrowser.ObjectForScripting = provider;
            provider.Associated = this;
        }

        public void Execute(string js, bool onload = false) {
            try {
                WebBrowser.InvokeScript("eval", onload ?
                        "window.addEventListener('load', function(){" + js + "}, false);" :
                        "(function(){" + js + "})();");
            } catch (InvalidOperationException e) {
                Logging.Warning("[WebBrowserBlock] Execute() InvalidOperationException: " + e.Message);
            } catch (COMException e) {
                Logging.Warning("[WebBrowserBlock] Execute() COMException: " + e.Message);
            } catch (Exception e) {
                Logging.Warning("[WebBrowserBlock] Execute(): " + e);
            }
        }

        public static readonly DependencyProperty UserAgentProperty = DependencyProperty.Register(nameof(UserAgent), typeof(string),
                typeof(WebBrowserBlock), new PropertyMetadata(OnUserAgentChanged));

        public string UserAgent {
            get { return (string)GetValue(UserAgentProperty); }
            set { SetValue(UserAgentProperty, value); }
        }

        private static void OnUserAgentChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            WebBrowserHelper.SetUserAgent((string)e.NewValue);
        }

        public static readonly DependencyProperty UserStyleProperty = DependencyProperty.Register(nameof(UserStyle), typeof(string),
                typeof(WebBrowserBlock), new PropertyMetadata(OnUserStyleChanged));

        public string UserStyle {
            get { return (string)GetValue(UserStyleProperty); }
            set { SetValue(UserStyleProperty, value); }
        }

        private static void OnUserStyleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBrowserBlock)o).OnUserStyleChanged((string)e.NewValue);
        }

        private void OnUserStyleChanged(string newValue) {
            SetUserStyle(newValue);
        }

        private void SetUserStyle(string userStyle) {
            if (string.IsNullOrWhiteSpace(userStyle)) return;

            const string jsMark = "/* JS part:";
            string jsPart = null;
            if (userStyle.Contains(jsMark)) {
                var splitted = userStyle.Split(new[] { jsMark }, StringSplitOptions.None);
                userStyle = splitted[0];
                jsPart = splitted[1];
            }

            Execute(@"
var s = document.getElementById('__cm_style');
if (s) s.parentNode.removeChild(s);
s = document.createElement('style');
s.id = '__cm_style';
s.innerHTML = '" + (userStyle.Replace("\r", "").Replace("\n", "\\n").Replace("'", "\\'")) + @"';
if (document.body){
    document.body.appendChild(s);
    " + (jsPart ?? "") + @"
} else {
    var p = document.createElement('style');
    p.innerHTML = 'body{display:none!important}html{background:black!important}'
    document.head.appendChild(p);

    function onload(){
        if (s.parentNode == document.head){
            document.head.removeChild(p);
            document.head.removeChild(s);
            document.body.appendChild(s);
            " + (jsPart ?? "") + @"
        }
    }

    document.head.appendChild(s);
    document.addEventListener('DOMContentLoaded', onload, false);
    window.addEventListener('load', onload, false);
}");
        }

        public static readonly DependencyProperty StartPageProperty = DependencyProperty.Register(nameof(StartPage), typeof(string),
                typeof(WebBrowserBlock), new PropertyMetadata(OnStartPageChanged));

        public string StartPage {
            get { return (string)GetValue(StartPageProperty); }
            set { SetValue(StartPageProperty, value); }
        }

        private static void OnStartPageChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBrowserBlock)o).OnStartPageChanged((string)e.NewValue);
        }

        private void OnStartPageChanged(string newValue) {
            Navigate(newValue);
        }

        public void Navigate(string url) {
            try {
                WebBrowser.Navigate(url);
            } catch (Exception e) {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                    url = "http://" + url;
                    try {
                        WebBrowser.Navigate(url);
                    } catch (Exception ex) {
                        Logging.Write("[WebBrowserBlock] Navigation failed: " + ex);
                    }
                } else {
                    Logging.Write("[WebBrowserBlock] Navigation failed: " + e);
                }
            }
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

        internal void ModifyPage() {
            WebBrowserHelper.SetSilent(WebBrowser, true);
            CommandManager.InvalidateRequerySuggested();
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
            SetUserStyle(UserStyle);
            PageLoaded?.Invoke(WebBrowser, new PageLoadedEventArgs(WebBrowser.Source.OriginalString));
        }

        private void WebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            UrlTextBox.Text = e.Uri.OriginalString;
            ModifyPage();
        }

        private void WebBrowser_OnLoadCompleted(object sender, NavigationEventArgs e) {}

        private void WebBrowser_OnNavigating(object sender, NavigatingCancelEventArgs e) {}

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ((WebBrowser != null) && (WebBrowser.CanGoBack));
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            WebBrowser.GoBack();
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = (WebBrowser != null) && WebBrowser.CanGoForward;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            WebBrowser.GoForward();
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

        private DispatcherTimer _timer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;
            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(0.5),
                IsEnabled = true
            };

            _timer.Tick += OnTick;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_timer == null) return;
            _timer.IsEnabled = false;
            _timer = null;
        }

        private void OnTick(object sender, EventArgs e) {
            Execute(@"if (!window.__cm_loaded){ window.external.FixPage(); }");
        }
    }
}
