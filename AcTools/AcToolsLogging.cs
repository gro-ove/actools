using System;
using System.Runtime.CompilerServices;

namespace AcTools {
    public static class AcToolsLogging {
        public static Action<string, string, string, int> Logger;

        public static void Write(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            if (Logger == null) {
                Console.WriteLine(s?.ToString() ?? "<NULL>");
            } else {
                Logger.Invoke(s?.ToString() ?? "<NULL>", m, p, l);
            }
        }
    }
}