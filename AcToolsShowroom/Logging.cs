using System;
using System.Diagnostics;
using System.IO;

namespace AcToolsShowroom {
    public static class Logging {
        private static string _filename;

        public static void Initialize(string filename) {
            _filename = filename;
            using (var file = new StreamWriter(_filename, false)) {
                file.WriteLine(DateTime.Now + ": " + "initialized");
            }
        }

        public static bool IsInitialized() {
            return _filename != null;
        }

        public static void Write(string s) {
            if (!IsInitialized()) return;
            try {
                using (var file = new StreamWriter(_filename, true)) {
                    file.WriteLine(DateTime.Now + ": " + s);
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
