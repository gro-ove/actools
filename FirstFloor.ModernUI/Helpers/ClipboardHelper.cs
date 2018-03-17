using System;
using System.Threading;
using System.Windows;

namespace FirstFloor.ModernUI.Helpers {
    public static class ClipboardHelper {
        public static void SetText(string text) {
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
        }
    }
}