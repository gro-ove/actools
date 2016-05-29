using System;
using System.Diagnostics;
using System.IO;

namespace AcTools.Render.Temporary {
    public static class RenderLogging {
        public static void Initialize(string filename, bool appendMode = false) {
            Logging.Initialize(filename, appendMode);
        }
    }

    internal static class Logging {
        private static string _filename;
        private static int _entries;

        // just for in case
        private const int EntriesLimit = 2000;

        internal static void Initialize(string filename, bool appendMode = false) {
            _filename = filename;
            if (!appendMode) {
                using (var file = new StreamWriter(_filename, false)) {
                    file.WriteLine(DateTime.Now + ": " + "initialized");
                }
            }
        }

        public static bool IsInitialized() {
            return _filename != null;
        }

        private static readonly object Locker = new object();

        public static void Write(string s) {
            Debug.WriteLine(s);

            if (!IsInitialized()) return;
            if (++_entries > EntriesLimit) return;

            try {
                lock (Locker) {
                    using (var writer = new StreamWriter(_filename, true)) {
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
