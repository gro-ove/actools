using System;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using CefSharp;
using FirstFloor.ModernUI;

namespace AcManager.Controls.UserControls.Cef {
    internal class LifeSpanHandler : ILifeSpanHandler {
        private readonly NewWindowsBehavior _mode;
        private readonly Func<string, bool> _newWindowCancelCallback;

        public LifeSpanHandler(NewWindowsBehavior mode, Func<string, bool> newWindowCancelCallback) {
            _mode = mode;
            _newWindowCancelCallback = newWindowCancelCallback;
        }

        public bool OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition,
                bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess,
                out IWebBrowser newBrowser) {
            switch (_mode) {
                case NewWindowsBehavior.OpenInBrowser:
                    WindowsHelper.ViewInBrowser(targetUrl);
                    break;
                case NewWindowsBehavior.Ignore:
                    break;
                case NewWindowsBehavior.ReplaceCurrent:
                    browser.MainFrame.LoadUrl(targetUrl);
                    break;
                case NewWindowsBehavior.MultiTab:
                    ActionExtension.InvokeInMainThread(() => _newWindowCancelCallback(targetUrl));
                    break;
                case NewWindowsBehavior.Callback:
                    var cancel = ActionExtension.InvokeInMainThread(() => _newWindowCancelCallback(targetUrl));
                    if (!cancel) {
                        browser.MainFrame.LoadUrl(targetUrl);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            newBrowser = null;
            return true;
        }

        public void OnAfterCreated(IWebBrowser browserControl, IBrowser browser) {}

        public bool DoClose(IWebBrowser browserControl, IBrowser browser) {
            return false;
        }

        public void OnBeforeClose(IWebBrowser browserControl, IBrowser browser) {}
    }
}