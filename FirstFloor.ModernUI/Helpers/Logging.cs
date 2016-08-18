using System;
using System.IO;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
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
                file.WriteLine(Time() + ": Initialized: " + DateTime.Now);
            }
        }

        public static bool IsInitialized() {
            return Filename != null;
        }

        private static readonly object Locker = new object();

        private static void WriteInner(char c, string s) {
            System.Diagnostics.Debug.WriteLine(s);

            if (!IsInitialized()) return;
            if (++_entries > EntriesLimit) return;

            try {
                lock (Locker) {
                    using (var writer = new StreamWriter(Filename, true)) {
                        writer.WriteLine($"{Time()}: {c} {s}");
                    }
                }
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine("[LOGGING EXCEPTION] " + e);
            }
        }

        public static void Write([LocalizationRequired(false)] string s) {
            WriteInner('→', s);
        }

        [StringFormatMethod(@"format")]
        public static void Write([LocalizationRequired(false)] string format, [LocalizationRequired(false)] params object[] args) {
            Write(args.Length == 0 ? format : string.Format(format, args));
        }

        [StringFormatMethod(@"format")]
        public static void Debug([LocalizationRequired(false)] string format, [LocalizationRequired(false)] params object[] args) {
            WriteInner('…', args.Length == 0 ? format : string.Format(format, args));
        }

        [StringFormatMethod(@"format")]
        public static void Warning([LocalizationRequired(false)] string format, [LocalizationRequired(false)] params object[] args) {
            WriteInner('⚠', args.Length == 0 ? format : string.Format(format, args));
        }

        [StringFormatMethod(@"format")]
        public static void Error([LocalizationRequired(false)] string format, [LocalizationRequired(false)] params object[] args) {
            WriteInner('×', args.Length == 0 ? format : string.Format(format, args));
        }
    }
}
