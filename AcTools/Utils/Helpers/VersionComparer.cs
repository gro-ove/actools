using System.Collections.Generic;

namespace AcTools.Utils.Helpers {
    public class VersionComparer : IComparer<string> {
        public static VersionComparer Instance = new VersionComparer();

        public int Compare(string x, string y) {
            return x.CompareAsVersionTo(y);
        }
    }
}