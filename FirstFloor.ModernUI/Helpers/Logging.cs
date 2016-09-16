using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public static class Logging {
        public static string Filename { get; private set; }

        private static int _entries;

        // just in case
        private const int EntriesLimit = 2000;

        private static string Time() {
            var t = DateTime.Now;
            return $"{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}.{t.Millisecond:D3}";
        }

        [CanBeNull]
        private static StreamWriter _streamWriter;

        private static void WriteLine(string s, bool append) {
            try {
                lock (Locker) {
                    if (_streamWriter != null) {
                        _streamWriter.WriteLine(s);
                    } else {
                        using (var writer = new StreamWriter(Filename, append)) {
                            writer.WriteLine(s);
                        }
                    }
                }
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine($"[LOGGING EXCEPTION] {e}");
            }
        }

        public static void Initialize(string filename, bool keepStream) {
            Filename = filename;

            if (keepStream) {
                try {
                    _streamWriter = new StreamWriter(File.Open(Filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                } catch (Exception e) {
                    System.Diagnostics.Debug.WriteLine($"[LOGGING INITIALIZE EXCEPTION] {e}");
                }
            }

            WriteLine($"{Time()}: Initialized", false);
        }

        public static void Flush() {
            try {
                lock (Locker) {
                    if (_streamWriter == null) return;
                    _streamWriter.Flush();
                    _streamWriter.Dispose();
                    _streamWriter = null;
                }
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine($"[LOGGING FLUSH EXCEPTION] {e}");
            }
        }

        public static bool IsInitialized() {
            return Filename != null;
        }

        private static readonly object Locker = new object();

        internal static void Write(char c, [CanBeNull] object o, string m, string p, int l) {
            var s = o?.ToString().Replace("\n", "\n\t") ?? "<NULL>";
            if (m != null && (s.Length == 0 || s[0] != '[')) {
                if (p != null) {
                    p = Path.GetFileNameWithoutExtension(p);
                    if (p.EndsWith(".xaml")) p = p.Substring(0, p.Length - 5);
                }

                s = $"[{p}:{l}] {m}(): {s}";
            }

            var n = $"{Time()}: {c} {s}";
            System.Diagnostics.Debug.WriteLine(n);
            if (IsInitialized() && ++_entries <= EntriesLimit) {
                WriteLine(n, true);
            }
        }

        public static void Write(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('→', s, m, p, l);
        }

        public static void Debug(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('…', s, m, p, l);
        }

        public static void Warning(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('⚠', s, m, p, l);
        }

        public static void Error(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('×', s, m, p, l);
        }
    }
}
