using System;
using System.Diagnostics;
using System.IO;

namespace FirstFloor.ModernUI.Helpers {
    public static class Logging {
        public static string Filename { get; private set; }

        private static int _entries;

        // just for in case
        private const int EntriesLimit = 2000;

        public static void Initialize(string filename) {
            Filename = filename;
            using (var file = new StreamWriter(Filename, false)) {
                file.WriteLine(DateTime.Now + ": " + "initialized");
            }
        }

        public static bool IsInitialized() {
            return Filename != null;
        }

        private static readonly object Locker = new object();

        public static void Write(string s) {
            Debug.WriteLine(s);

            if (!IsInitialized()) return;
            if (++_entries > EntriesLimit) return;

            try {
                lock (Locker) {
                    using (var writer = new StreamWriter(Filename, true)) {
                        writer.WriteLine(DateTime.Now + ": " + s);
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine("[LOGGING EXCEPTION] " + e);
            }
        }

        public static void Write(string format, params object[] args) {
            Write(args.Length == 0 ? format : string.Format(format, args));
        }

        public static void Warning(string format, params object[] args) {
            Write("[WARNING] " + (args.Length == 0 ? format : string.Format(format, args)));
        }

        public static void Error(string format, params object[] args) {
            Write("[ERROR] " + (args.Length == 0 ? format : string.Format(format, args)));
        }
    }
}
