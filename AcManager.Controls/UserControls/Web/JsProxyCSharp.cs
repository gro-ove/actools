using System.Runtime.InteropServices;
using System.Security.Permissions;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public abstract class JsProxyCSharp : JsProxyBase {
        protected JsProxyCSharp(JsBridgeBase bridge) : base(bridge) { }

        [UsedImplicitly]
        public void Log(string message) {
            Logging.Write("" + message);
        }

        [UsedImplicitly]
        public void OnError(string error, string url, int line, int column) {
            Sync(() => {
                Logging.Warning($"[{url}:{line}:{column}] {error}");
                Tab()?.OnError(error, url, line, column);
            });
        }

        [UsedImplicitly]
        public void Alert(string message) {
            Sync(() => ModernDialog.ShowMessage(message));
        }

        [UsedImplicitly]
        public string Prompt(string message, string defaultValue) {
            return Sync(() => FirstFloor.ModernUI.Dialogs.Prompt.Show(message, ControlsStrings.WebBrowser_Prompt, defaultValue));
        }

        [UsedImplicitly]
        public object CmTest() {
            return true;
        }
    }
}