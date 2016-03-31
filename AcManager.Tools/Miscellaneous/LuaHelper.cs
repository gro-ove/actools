using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using NLua;

namespace AcManager.Tools.Miscellaneous {
    public static class LuaHelper {
        public static bool CompareStrings(string a, string b) {
            return string.Equals(a, b, StringComparison.InvariantCulture);
        }

        public static bool CompareStringsIgnoringCase(string a, string b) {
            return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
        }

        public static double GetNumberValue(string s) {
            return FlexibleParser.TryParseDouble(s) ?? double.NaN;
        }

        public static void Log(params object[] str) {
            Logging.Write("Lua: " + str.Select(x => $"“{x}”").JoinToString(", "));
        }

        public static Lua GetExtended() {
            var result = new Lua();
            result.LoadCLRPackage();
            result.DoString(@"
import ('AcManager.Tools', 'AcManager.Tools.Miscellaneous')
log = LuaHelper.Log
numutils = {}
numutils.numvalue = LuaHelper.GetNumberValue
strutils = {}
strutils.equals = LuaHelper.CompareStrings
strutils.equals_i = LuaHelper.CompareStringsIgnoringCase");
            return result;
        }
    }
}
