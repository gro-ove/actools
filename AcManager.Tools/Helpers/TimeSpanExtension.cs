using System;

namespace AcManager.Tools.Helpers {
    public static class TimeSpanExtension {
        public static string ToProperString(this TimeSpan span) {
            return $"{span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}
