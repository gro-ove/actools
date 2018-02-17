using System;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using CefSharp;
using FirstFloor.ModernUI;

namespace AcManager.Controls.UserControls.CefSharp {
    internal class LifeSpanHandler : ILifeSpanHandler {
        private readonly NewWindowsBehavior _mode;
        private readonly Action<string> _newWindowCallback;

        public LifeSpanHandler(NewWindowsBehavior mode, Action<string> newWindowCallback) {
            _mode = mode;
            _newWindowCallback = newWindowCallback;
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
                    ActionExtension.InvokeInMainThread(() => _newWindowCallback(targetUrl));
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