using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;

namespace CustomTracksBakery {
    public static class Extensions {
        private static string PluralizeExt(int v, string s) {
            return v.ToInvariantString() + " " + (v == 1 ? s : s + "s");
        }

        public static string ToReadableTime(this TimeSpan span, bool considerMilliseconds = false) {
            var result = new List<string>();

            var days = (int)span.TotalDays;
            var months = days / 30;
            if (months > 30) {
                result.Add(PluralizeExt(months, "month"));
                days = days % 30;
            }

            if (days > 0) {
                result.Add(days % 7 == 0 ? PluralizeExt(days / 7, "week") : PluralizeExt(days, "day"));
            }

            if (span.Hours > 0 && months == 0) {
                result.Add(PluralizeExt(span.Hours, "hour"));
            }

            if (span.Minutes > 0 && months == 0) {
                result.Add(PluralizeExt(span.Minutes, "minute"));
            }

            if (span.Seconds > 0 && span.Hours == 0 && months == 0 && days == 0) {
                result.Add(PluralizeExt(span.Seconds, "second"));
            }

            if (considerMilliseconds && span.Milliseconds > 0 && result.Count == 0) {
                result.Add($@"{span.Milliseconds} ms");
            }

            return result.Count > 0 ? string.Join(@" ", result.Take(2)) : PluralizeExt(0, "second");
        }
    }
}