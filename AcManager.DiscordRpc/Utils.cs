using System;
using System.Runtime.CompilerServices;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.DiscordRpc {
    internal static class Utils {
        public static void Log(string message, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            if (DiscordConnector.OptionVerboseMode) {
                Logging.Debug(message, m, p, l);
            }
        }

        public static void Warn(string message, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            Logging.Warning(message, m, p, l);
        }

        [NotNull]
        public static string Limit([CanBeNull] this string s, int length, [NotNull] string alternativeValue) {
            return string.IsNullOrWhiteSpace(s) ? alternativeValue : s.Length > length ? s.Substring(0, length) : s;
        }

        public static long ToTimestamp(this DateTime dt) {
            return (dt.Ticks - 621355968000000000) / 10000000;
        }
    }
}