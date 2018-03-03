using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using CefSharp;
using CefSharp.Wpf;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;

namespace AcManager.Controls.UserControls.Cef {
    internal class MenuHandler : IContextMenuHandler {
        void IContextMenuHandler.OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters,
                IMenuModel model) { }

        bool IContextMenuHandler.OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters,
                CefMenuCommand commandId, CefEventFlags eventFlags) {
            return false;
        }

        void IContextMenuHandler.OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame) {
            var chromium = (ChromiumWebBrowser)browserControl;
            chromium.Dispatcher.Invoke(() => { chromium.ContextMenu = null; });
        }

        private static CefMenuCommand[] GetMenuItems(IMenuModel model) {
            var result = new CefMenuCommand[model.Count];
            for (var i = 0; i < model.Count; i++) {
                result[i] = model.GetCommandIdAt(i);
            }
            return result;
        }

        bool IContextMenuHandler.RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model,
                IRunContextMenuCallback callback) {
            if (model.Count == 0) {
                // No menu if 0 items (flash)
                return true;
            }

            var chromium = (ChromiumWebBrowser)browserControl;

            //IMenuModel is only valid in the context of this method, so need to read the values before invoking on the UI thread
            var menuItems = GetMenuItems(model);

            ActionExtension.InvokeInMainThread(() => {
                var contextMenu = new ContextMenu();

                if (menuItems.ArrayContains(CefMenuCommand.Cut)) {
                    contextMenu.Items.Add(new MenuItem { Header = "Cut", Command = chromium.CutCommand });
                }

                if (menuItems.ArrayContains(CefMenuCommand.Copy)) {
                    contextMenu.Items.Add(new MenuItem { Header = "Copy", Command = chromium.CopyCommand });
                }

                if (menuItems.ArrayContains(CefMenuCommand.Paste)) {
                    contextMenu.Items.Add(new MenuItem { Header = "Paste", Command = chromium.PasteCommand });
                }

                if (contextMenu.Items.Count > 1) {
                    contextMenu.Items.Add(new Separator());
                }

                if (menuItems.ArrayContains(CefMenuCommand.SelectAll)) {
                    contextMenu.Items.Add(new MenuItem { Header = "Select all", Command = chromium.SelectAllCommand });
                }

                if (contextMenu.Items.Count > 0) {
                    contextMenu.Items.Add(new Separator());
                }

                contextMenu.Items.Add(new MenuItem { Header = "Back", Command = new DelegateCommand(browser.GoBack, () => !browser.IsDisposed && browser.CanGoBack) });
                contextMenu.Items.Add(new MenuItem { Header = "Forward", Command = new DelegateCommand(browser.GoForward, () => !browser.IsDisposed && browser.CanGoForward) });
                contextMenu.Items.Add(new MenuItem { Header = "Refresh", Command = new DelegateCommand(() => browser.Reload(true)) });
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(new MenuItem {
                    Header = "Open page in default browser",
                    Command = new DelegateCommand<string>(WindowsHelper.ViewInBrowser),
                    CommandParameter = frame.Url
                });
                contextMenu.Items.Add(new MenuItem {
                    Header = "Show developer tools",
                    Command = new DelegateCommand(browser.ShowDevTools)
                });

                chromium.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            });

            callback.Cancel();
            return true;
        }
    }
}