using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AcTools.Utils {
    public static class AcKunosContent {
        public static IEnumerable<string> GetKunosCarIds(string acRoot) {
            return File.ReadAllLines(Path.Combine(acRoot, "content", "sfx", "GUIDs.txt")).Select(x => {
                var i = x.IndexOf(" event:/cars/", 38, StringComparison.Ordinal);
                if (i == -1) return null;
                var e = x.IndexOf('/', i + 13);
                return e == -1 ? x.Substring(i + 13) : x.Substring(i + 13, e - i - 13);
            }).Where(x => x != null).Distinct();
        }
    }
}