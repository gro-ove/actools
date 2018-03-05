using System.Linq;
using System.Windows;
using System.Windows.Input;
using CefSharp;
using FirstFloor.ModernUI;

namespace AcManager.Controls.UserControls.Cef {
    internal class KeyboardHandler : IKeyboardHandler {
        public bool OnPreKeyEvent(IWebBrowser browserControl, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers,
                bool isSystemKey, ref bool isKeyboardShortcut) {
            return false;
        }

        public bool OnKeyEvent(IWebBrowser browserControl, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers,
                bool isSystemKey) {
            if (type != KeyType.RawKeyDown || !modifiers.HasFlag(CefEventFlags.ControlDown) && !modifiers.HasFlag(CefEventFlags.AltDown)) return false;
            return ActionExtension.InvokeInMainThread(() => OnKeyDown(browserControl, windowsKeyCode));
        }

        private static bool OnKeyDown(IWebBrowser browserControl, int windowsKeyCode) {
            var window = browserControl is CefSharp.WinForms.ChromiumWebBrowser formsBrowser ? formsBrowser.FindParentWindow()
                    : browserControl is CefSharp.Wpf.ChromiumWebBrowser wpfBrowser ? Window.GetWindow(wpfBrowser) : null;
            return window != null && ExecuteInputBinding(windowsKeyCode, window);
        }

        private static bool ExecuteInputBinding(int windowsKeyCode, UIElement window) {
            var key = KeyInterop.KeyFromVirtualKey(windowsKeyCode);
            var modifiers = Keyboard.Modifiers;
            var binding = window.InputBindings.OfType<InputBinding>().FirstOrDefault(x =>
                    x.Gesture is KeyGesture gesture && gesture.Key == key && gesture.Modifiers == modifiers);
            if (binding == null || !binding.Command.CanExecute(binding.CommandParameter)) return false;
            binding.Command.Execute(binding.CommandParameter);
            return true;
        }
    }
}