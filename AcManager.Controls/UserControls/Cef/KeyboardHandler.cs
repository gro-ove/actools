using System.Linq;
using System.Windows;
using System.Windows.Input;
using CefSharp;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Windows.Attached;

namespace AcManager.Controls.UserControls.Cef {
    internal class KeyboardHandler : IKeyboardHandler {
        public bool OnPreKeyEvent(IWebBrowser browserControl, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers,
                bool isSystemKey, ref bool isKeyboardShortcut) {
            return false;
        }

        public bool OnKeyEvent(IWebBrowser browserControl, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers,
                bool isSystemKey) {
            return type == KeyType.RawKeyDown && ActionExtension.InvokeInMainThread(() => OnKeyDown(browserControl, windowsKeyCode, modifiers));
        }

        private static bool OnKeyDown(IWebBrowser browserControl, int windowsKeyCode, CefEventFlags modifiers) {
            var control = browserControl is CefSharp.WinForms.ChromiumWebBrowser formsBrowser
                    ? (FrameworkElement)formsBrowser.FindWpfHost()
                    : browserControl as CefSharp.Wpf.ChromiumWebBrowser;
            var window = control == null ? null : Window.GetWindow(control);
            if (window == null) return false;

            var key = KeyInterop.KeyFromVirtualKey(windowsKeyCode);
            if (key == Key.Escape) {
                control.RemoveFocus();
                return true;
            }

            return (modifiers.HasFlag(CefEventFlags.ControlDown) || modifiers.HasFlag(CefEventFlags.AltDown)) && ExecuteInputBinding(key, window);
        }

        private static bool ExecuteInputBinding(Key key, UIElement window) {
            var modifiers = Keyboard.Modifiers;
            var binding = window.InputBindings.OfType<InputBinding>().FirstOrDefault(x =>
                    x.Gesture is KeyGesture gesture && gesture.Key == key && gesture.Modifiers == modifiers);
            if (binding == null || !binding.Command.CanExecute(binding.CommandParameter)) return false;
            binding.Command.Execute(binding.CommandParameter);
            return true;
        }
    }
}