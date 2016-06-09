using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using AcManager.Tools.Helpers;
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
            return Controls.Pages.Dialogs.Prompt.Show(message, "Webpage says", defaultValue);
        }

        public object CmTest() {
            return true;
        }
    }

    public partial class WebBrowserBlock {
        public WebBrowserBlock() {
            InitializeComponent();
        }

        public WebBrowser Inner => WebBrowser;

        public void SetScriptProvider(object provider) {
            WebBrowser.ObjectForScripting = provider;
        }

        public void Execute(string js) {
            WebBrowser.InvokeScript("eval", $"(function(){{ {js} }})();");
        }

        public void SetUserStyle(string userStyle) {
            Execute(@"
var s = document.getElementById('__cm_style');
if (s) document.head.removeChild(s);
s = document.createElement('style');
s.id = '__cm_style';
document.head.appendChild(s);
s.innerHTML = '" + userStyle.Replace("\r", "").Replace("\n", "\\n").Replace("'", "\\'") + @"';
window.addEventListener('load', function(){ document.head.removeChild(s); document.body.removeChild(s); }, false);");
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
            WebBrowser.Navigate(newValue);
        }

        public event NavigatedEventHandler Navigated;

        private void UrlTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                WebBrowser.Navigate(UrlTextBox.Text);
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
            Execute(@"window.onerror = function(err){ window.external.Log('' + err); };");
            Execute(@"document.addEventListener('mousedown', function(e){ if (e.target.getAttribute('target') == '_blank'){ e.target.setAttribute('target', '_parent'); } }, false);");
            Navigated?.Invoke(sender, e);
        }

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

        private void GoToPage_Executed(object sender, ExecutedRoutedEventArgs e) {
            WebBrowser.Navigate(UrlTextBox.Text);
        }

        private void BrowseHome_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void BrowseHome_Executed(object sender, ExecutedRoutedEventArgs e) {
            WebBrowser.Navigate(StartPage);
        }
    }
}
