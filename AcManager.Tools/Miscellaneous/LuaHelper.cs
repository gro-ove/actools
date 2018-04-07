using System;
using System.Linq;
using AcManager.Tools.Data;
using AcManager.Tools.Objects;
using AcTools.Processes;
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

        public static object ToMoonSharp<T>() where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().ToDictionary(x => x.ToString(), x => (int)(object)x);
        }

        [CanBeNull]
        public static Script GetExtended() {
            try {
                if (!_registered) {
                    UserData.RegisterType<CarObject>();
                    UserData.RegisterType<TrackObject>();
                    UserData.RegisterType<TrackExtraLayoutObject>();
                    UserData.RegisterType<TrackObjectBase>();
                    UserData.RegisterType<WeatherObject>();
                    UserData.RegisterType<TagsCollection>();
                    _registered = true;
                }

                var state = new Script();

                state.Globals["log"] = (Action<object[]>)Log;
                state.Globals["numutils"] = new Table(state) {
                    ["numvalue"] = (Func<string, double>)GetNumberValue
                };

                state.Globals["strutils"] = new Table(state) {
                    ["equals"] = (Func<string, string, bool>)CompareStrings,
                    ["equals_i"] = (Func<string, string, bool>)CompareStringsIgnoringCase
                };

                state.Globals[@"SessionType"] = ToMoonSharp<Game.SessionType>();
                return state;
            } catch (Exception e) {
                Logging.Warning("Can’t initialize: " + e);
                return null;
            }
        }

        public static bool AsBool(this DynValue value) {
            return (value.Type != DataType.String || value.String != @"0") && value.CastToBool();
        }
    }
}
