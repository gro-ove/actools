using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        private LocalLogging Logging = new LocalLogging();

        [Localizable(false)]
        private class LocalLogging {
            // private static int _id = 0;
            // private readonly int _instanceId = ++_id;

            private DpiAwareWindow _parent;

            public string Id => $@"W{_parent.GetHashCode():X8}; {_parent.Left}, {_parent.Top}, {_parent.ActualWidth}×{_parent.ActualHeight}";

            public void SetParent(DpiAwareWindow parent) {
                _parent = parent;
            }

            public void Debug(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
                if (!OptionVerboseMode) return;
                Helpers.Logging.Write('…', $"({Id}) {s?.ToString() ?? "<NULL>"}", m, p, l);
            }

            public void Here([CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
                if (!OptionVerboseMode) return;
                if (p != null) {
                    p = Path.GetFileNameWithoutExtension(p);
                    if (p.EndsWith(".xaml")) p = p.Substring(0, p.Length - 5);
                }
                Helpers.Logging.Write('⊕', $"[{p}:{l}] ({Id}) {m}", null, null, -1);
            }

            public void Error(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
                Helpers.Logging.Write('×', $"({Id}) {s?.ToString() ?? "<NULL>"}", m, p, l);
            }

            public T Log<T>(string s, T value, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
                if (OptionVerboseMode) {
                    Helpers.Logging.Write('…', $"({Id}) {s ?? "<NULL>"}: {value}", m, p, l);
                }
                return value;
            }
        }
    }
}