using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.UserControls {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [ComVisible(true)]
    public abstract class BaseScriptProvider {
        public void Log(string message) {
            Logging.Write("[ScriptProvider] " + message);
        }

        public void Alert(string message) {
            ModernDialog.ShowMessage(message);
        }

        public string Prompt(string message, string defaultValue) {
            return Dialogs.Prompt.Show(message, "Webpage says", defaultValue);
        }

        public object CmTest() {
            return true;
        }
    }

    public partial class WebBrowserBlock {
        #region Initialization
        public static readonly string UserAgent;

        static WebBrowserBlock() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? " WOW64;" : "")}";
            UserAgent = $"Mozilla/5.0 ({windows} ContentManager/{BuildInformation.AppVersion}) like Gecko";
        }
        #endregion

        public WebBrowserBlock() {
            InitializeComponent();
            WebBrowserHelper.SetUserAgent(UserAgent);
        }

        public WebBrowser Inner => WebBrowser;

        public void SetScriptProvider(object provider) {
            WebBrowser.ObjectForScripting = provider;
        }

        public void Execute(string js) {
            WebBrowser.InvokeScript("eval", $"(function(){{ {js} }})();");
        }

        public static readonly DependencyProperty UserStyleProperty = DependencyProperty.Register(nameof(UserStyle), typeof(string),
                typeof(WebBrowserBlock), new PropertyMetadata(OnUserStyleChanged));

        public string UserStyle {
            get { return (string)GetValue(UserStyleProperty); }
            set { SetValue(UserStyleProperty, value); }
        }

        private static void OnUserStyleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBrowserBlock)o).OnUserStyleChanged((string)e.OldValue, (string)e.NewValue);
        }

        private void OnUserStyleChanged(string oldValue, string newValue) {
            SetUserStyle(newValue);
        }

        private void SetUserStyle(string userStyle) {
            Execute(@"
var s = document.getElementById('__cm_style');
if (s) s.parentNode.removeChild(s);
s = document.createElement('style');
s.id = '__cm_style';
document.head.appendChild(s);
s.innerHTML = '" + (userStyle?.Replace("\r", "").Replace("\n", "\\n").Replace("'", "\\'") ?? "") + @"';
window.external.Log('setting userstyle');");
        }

        public static readonly DependencyProperty StartPageProperty = DependencyProperty.Register(nameof(StartPage), typeof(string),
                typeof(WebBrowserBlock), new PropertyMetadata(OnStartPageChanged));

        public string StartPage {
            get { return (string)GetValue(StartPageProperty); }
            set { SetValue(StartPageProperty, value); }
        }

        private static void OnStartPageChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBrowserBlock)o).OnStartPageChanged((string)e.OldValue, (string)e.NewValue);
        }

        private void OnStartPageChanged(string oldValue, string newValue) {
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

        public event NavigatedEventHandler Navigated;

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

        private void WebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            WebBrowserHelper.SetSilent(WebBrowser, true);
            UrlTextBox.Text = e.Uri.OriginalString;
            CommandManager.InvalidateRequerySuggested();
            Execute(@"window.onerror = function(err, url, lineNumber){ window.external.Log('error: `' + err + '` script: `' + url + '` line: ' + lineNumber); };");
            Execute(@"document.addEventListener('mousedown', function(e){ if (e.target.getAttribute('target') == '_blank'){ e.target.setAttribute('target', '_parent'); } }, false);");
            SetUserStyle(UserStyle);
            Navigated?.Invoke(sender, e);
        }

        private void WebBrowser_OnNavigating(object sender, NavigatingCancelEventArgs e) {}

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ((WebBrowser != null) && (WebBrowser.CanGoBack));
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            WebBrowser.GoBack();
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ((WebBrowser != null) && (WebBrowser.CanGoForward));
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
    }
}
