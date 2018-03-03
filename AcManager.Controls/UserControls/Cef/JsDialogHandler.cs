using CefSharp;

namespace AcManager.Controls.UserControls.Cef {
    internal class JsDialogHandler : IJsDialogHandler {
        public bool OnJSDialog(IWebBrowser browserControl, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText,
                IJsDialogCallback callback, ref bool suppressMessage) {
            return false;
        }

        public bool OnJSBeforeUnload(IWebBrowser browserControl, IBrowser browser, string message, bool isReload, IJsDialogCallback callback) {
            return false;
        }

        public void OnResetDialogState(IWebBrowser browserControl, IBrowser browser) {}

        public void OnDialogClosed(IWebBrowser browserControl, IBrowser browser) {}
    }
}