using System;

namespace AcTools.Utils.Helpers {
    public static class DateTimeExtension {
        public static long ToUnixTimestamp(this DateTime d) {
            return (long)(d.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static DateTime ToDateTime(this long l) {
            return (new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(l)).ToLocalTime();
        }

        public static DateTime ToDateTimeFromMilliseconds(this long l) {
            return (new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(l)).ToLocalTime();
        }

        public static long ToMillisecondsTimestamp(this DateTime d) {
            return (long)(d.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static bool IsSameDay(this DateTime a, DateTime b) {
            return CompareByDays(a, b) == 0;
        }

        public static int CompareByDays(this DateTime a, DateTime b) {
            var d = a.Year - b.Year;
            if (d != 0) return d;

            d = a.Month - b.Month;
            if (d != 0) return d;

            d = a.Day - b.Day;
            return d;
        }
    }
}
