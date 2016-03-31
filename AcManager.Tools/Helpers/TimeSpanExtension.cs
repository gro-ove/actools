using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Globalization;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {

    public static class TimeSpanExtension {
        public static string ToProperString(this TimeSpan span) {
            return $"{span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}
