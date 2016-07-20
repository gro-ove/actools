using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [ComVisible(true)]
    public abstract class BaseScriptProvider {
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
            Process.Start(url);
        }

        public void Log(string message) {
            Logging.Write("[ScriptProvider] " + message);
        }

        public void Alert(string message) {
            ModernDialog.ShowMessage(message);
        }

        public string Prompt(string message, string defaultValue) {
            return Dialogs.Prompt.Show(message, Resources.WebBrowser_Prompt, defaultValue);
        }

        public void FixPage() {
            Associated?.ModifyPage();
        }

        public object CmTest() {
            return true;
        }
    }
}