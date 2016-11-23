using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public abstract class ScriptProviderBase {
        private WeakReference<WebBlock> _lastAssociatedWebBrowser;

        [CanBeNull]
        public WebBlock Associated {
            get {
                WebBlock res = null;
                return _lastAssociatedWebBrowser?.TryGetTarget(out res) == true ? res : null;
            }
            set { _lastAssociatedWebBrowser = value == null ? null : new WeakReference<WebBlock>(value); }
        }

        public void NavigateTo(string url) {
            if (Associated?.OpenNewWindowsExternally == false) {
                Associated.Navigate(url);
            } else {
                WindowsHelper.ViewInBrowser(url);
            }
        }

        public void Log(string message) {
            Logging.Write("" + message);
        }

        public void OnError(string error, string url, int line, int column) {
            Logging.Warning($"[{url}:{line}:{column}] {error}");
            Associated?.OnError(error, url, line, column);
        }

        public void Alert(string message) {
            ModernDialog.ShowMessage(message);
        }

        public string Prompt(string message, string defaultValue) {
            return Dialogs.Prompt.Show(message, ControlsStrings.WebBrowser_Prompt, defaultValue);
        }

        public void FixPage() {
            Associated?.ModifyPage();
        }

        public object CmTest() {
            return true;
        }
    }
}