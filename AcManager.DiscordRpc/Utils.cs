using System;
using System.Runtime.CompilerServices;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.DiscordRpc {
    internal static class Utils {
        public static void Log(string message, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
#if DEBUG
            Logging.Debug(message, m, p, l);
#else
            if (DiscordConnector.OptionVerboseMode) {
                Logging.Debug(message, m, p, l);
            }
#endif
        }

        public static void Warn(string message, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Logging.Warning(message, m, p, l);
        }

        public static string Limit([CanBeNull] this string s, int length) {
            return s != null && s.Length > length ? s.Substring(0, length) : s;
        }

        public static long ToTimestamp(this DateTime dt) {
            return (dt.Ticks - 621355968000000000) / 10000000;
        }
    }
}