using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

        internal static readonly object HereMessage = new object();

        internal static void Write(char c, [CanBeNull] object o, string m, string p, int l) {
            if (p != null) {
                p = Path.GetFileNameWithoutExtension(p);
                if (p.EndsWith(".xaml")) p = p.Substring(0, p.Length - 5);
            }

            string s;
            if (ReferenceEquals(HereMessage, o)) {
                s = $"[{p}:{l}] {m}()";
            } else {
                s = o?.ToString().Replace("\n", "\n\t") ?? "<NULL>";

                if (s.IndexOf("%FROM%", StringComparison.OrdinalIgnoreCase) != -1) {
                    var frame = new StackTrace().GetFrame(3);
                    s = Regex.Replace(s, @"%(?:CALLEE|FROM)%",
                            _ => $"From: [{Path.GetFileName(frame.GetFileName())}:{frame.GetFileLineNumber()}] {frame.GetMethod().Name}");
                }


                if (m != null && (s.Length == 0 || s[0] != '[' || s.Length > 1 && !char.IsLetter(s[1]))) {
                    s = $"[{p}:{l}] {m}(): {s}";
                }
            }

            var n = $"{Time()}: {c} {s}";
            System.Diagnostics.Debug.WriteLine(n);
            if (IsInitialized() && ++_entries <= EntriesLimit) {
                WriteLine(n, true);
            }
        }

        // I have a small .tmLanguage file for highlighting those logs, feel free to contact me if you need it
        // or use this version: https://gist.github.com/gro-ove/4bf9e15c0e27aed7fef1309f9c544efb

        public static void Write(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('→', s, m, p, l);
        }

        public static void Debug(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('…', s, m, p, l);
        }

        public static void Caution(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('⇒', s, m, p, l);
        }

        public static void Here([CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('⊕', HereMessage, m, p, l);
        }

        public static void Warning(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('⚠', s, m, p, l);
        }

        public static void Error(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('×', s, m, p, l);
        }

        public static void Unexpected(object s = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Write('☠', s, m, p, l);
        }
    }
}
