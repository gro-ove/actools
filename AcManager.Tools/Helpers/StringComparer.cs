using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers {
    public static class StringComparer {
        public static int CompareToExt(this string a, string b) {
            return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
