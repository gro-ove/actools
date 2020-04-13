using System;
using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Helpers {
    public static class ClipboardHelper {
        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool SetClipboardData(uint uFormat, IntPtr data);

        private const uint CF_UNICODETEXT = 13;

        public static bool SetText(string text) {
            if (!OpenClipboard(IntPtr.Zero)) {
                return false;
            }

            var global = Marshal.StringToHGlobalUni(text);
            SetClipboardData(CF_UNICODETEXT, global);
            CloseClipboard();
            return true;
        }

        /*public static void SetText(string text) {
            Exception exception = null;
            for (var i = 0; i < 5; i++) {
                try {
                    Clipboard.SetText(text);
                    return;
                } catch (Exception e) {
                    Thread.Sleep(10);
                    exception = e;
                }
            }

            NonfatalError.Notify("Can’t copy text", "No access to clipboard.", exception);
        }*/
    }
}