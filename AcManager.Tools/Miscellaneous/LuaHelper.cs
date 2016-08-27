using System;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

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

        private static bool _registered;

        [CanBeNull]
        public static Script GetExtended() {
            try {
                if (!_registered) {
                    UserData.RegisterAssembly();
                    _registered = true;
                }

                var script = new Script();

                script.Globals["log"] = (Action<object[]>)Log;
                script.Globals["numutils"] = new Table(script) {
                    ["numvalue"] = (Func<string, double>)GetNumberValue
                };
                script.Globals["strutils"] = new Table(script) {
                    ["equals"] = (Func<string, string, bool>)CompareStrings,
                    ["equals_i"] = (Func<string, string, bool>)CompareStringsIgnoringCase
                };

                return script;
            } catch (Exception e) {
                Logging.Warning("Can’t initialize: " + e);
                return null;
            }
        }
    }
}
