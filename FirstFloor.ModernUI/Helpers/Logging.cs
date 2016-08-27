using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public static class Logging {
        public static string Filename { get; private set; }

        private static int _entries;

        // just for in case
        private const int EntriesLimit = 2000;

        private static string Time() {
            var t = DateTime.Now;
            return $"{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}.{t.Millisecond:D3}";
        }

        public static void Initialize(string filename) {
            Filename = filename;
            using (var file = new StreamWriter(Filename, false)) {
                file.WriteLine($"{Time()}: Initialized");
            }
        }

        public static bool IsInitialized() {
            return Filename != null;
        }

        private static readonly object Locker = new object();

        private static void WriteInner(char c, [CanBeNull] string s, string m, string p, int l) {
            s = s?.Replace("\n", "\n\t") ?? "<NULL>";
            if (p != null) {
                p = Path.GetFileNameWithoutExtension(p);
                if (p.EndsWith(".xaml")) p = p.Substring(0, p.Length - 5);
            }

            System.Diagnostics.Debug.WriteLine(s);

            if (!IsInitialized()) return;
            if (++_entries > EntriesLimit) return;

            try {
                lock (Locker) {
                    using (var writer = new StreamWriter(Filename, true)) {
                        writer.WriteLine(m == null || s.Length > 0 && s[0] == '[' ? $"{Time()}: {c} {s}" :
                                p == null || l == -1 ? $"{Time()}: {c} {m}(): {s}" : $"{Time()}: {c} [{p}:{l}] {m}(): {s}");
                    }
                }
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine($"[LOGGING EXCEPTION] {e}");
            }
        }

        public static void Write(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            WriteInner('→', s?.ToString(), m, p, l);
        }

        public static void Debug(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            WriteInner('…', s?.ToString(), m, p, l);
        }

        public static void Warning(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            WriteInner('⚠', s?.ToString(), m, p, l);
        }

        public static void Error(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            WriteInner('×', s?.ToString(), m, p, l);
        }
    }
}
