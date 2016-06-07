using System;

namespace AcManager.Tools.Helpers {
    public static class StringComparer {
        public static int InvariantCompareTo(this string a, string b) {
            return string.Compare(a, b, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
