using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Threading;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.UserControls.Web {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public abstract class ScriptProviderBase {
        protected void Sync(Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
                try {
                    action();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            });
        }

        protected T Sync<T>(Func<T> action) {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
                try {
                    return action();
                } catch (Exception e) {
                    Logging.Warning(e);
                    return default(T);
                }
            });
        }

        protected abstract ScriptProviderBase ForkForOverride(WebTab tab);

        public WebTab Tab { get; private set; }

        public ScriptProviderBase ForkFor(WebTab tab) {
            Tab = tab;
            return ForkForOverride(tab);
        }

        public void NavigateTo(string url) {
            Sync(() => {
                /*if (Associated?.NewWindowsBehavior == ) {
                    Associated.Navigate(url);
                } else {
                    WindowsHelper.ViewInBrowser(url);
                }*/
            });
        }

        public void Log(string message) {
            Logging.Write("" + message);
        }

        public void OnError(string error, string url, int line, int column) {
            Sync(() => {
                Logging.Warning($"[{url}:{line}:{column}] {error}");
                Tab?.OnError(error, url, line, column);
            });
        }

        public void Alert(string message) {
            Sync(() => {
                ModernDialog.ShowMessage(message);
            });
        }

        public string Prompt(string message, string defaultValue) {
            return Sync(() => FirstFloor.ModernUI.Dialogs.Prompt.Show(message, ControlsStrings.WebBrowser_Prompt, defaultValue));
        }

        public object CmTest() {
            return true;
        }
    }
}