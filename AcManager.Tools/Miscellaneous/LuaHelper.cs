using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using AcManager.Tools.Data;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using PowerLineStatus = System.Windows.Forms.PowerLineStatus;

namespace AcManager.Tools.Miscellaneous {
    public static class LuaHelper {
        public static bool CompareStrings(string a, string b) {
            return string.Equals(a, b, StringComparison.InvariantCulture);
        }
        
        public static bool MatchStrings(string pattern, string str) {
            if (string.IsNullOrEmpty(str)) return false;
            return new Regex("^" + Regex.Escape(pattern).Replace(@"\*", @".*").Replace(@"\?", @".") + "$").IsMatch(str);
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

        public static object ToMoonSharp<T, TCast>() where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().ToDictionary(x => x.ToString(), x => (TCast)(object)x);
        }

        [CanBeNull]
        public static Script GetExtended(bool pcState = false) {
            try {
                if (!_registered) {
                    UserData.RegisterType<CarObject>();
                    UserData.RegisterType<CarSkinObject>();
                    UserData.RegisterType<TrackObject>();
                    UserData.RegisterType<TrackSkinObject>();
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
                    ["split"] = (Func<string, string, string[]>)((i, s) => i.Split(new[]{ s }, StringSplitOptions.None)),
                    ["equals"] = (Func<string, string, bool>)CompareStrings,
                    ["equals_i"] = (Func<string, string, bool>)CompareStringsIgnoringCase,
                    ["match"] = (Func<string, string, bool>)MatchStrings,
                };

                var sysUtils = new Table(state) {
                    ["message"] = (Action<string>)(s => {
                        Logging.Debug("Message from Lua:" + s);
                        ActionExtension.InvokeInMainThreadAsync(() => ModernDialog.ShowMessage(s.Or(@"<empty>"), "Message from a script", MessageBoxButton.OK));
                    }),
                };

                if (pcState) {
                    sysUtils["numdesktops"] = (Func<int>)User32.GetMonitorCount;
                    sysUtils["primarymonitor"] = (Func<string>)User32.GetPrimaryDisplayName;
                    sysUtils["discharging"] = (Func<bool>)(() => SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline);
                }
                state.Globals["sysutils"] = sysUtils;
                
                state.Globals[@"SessionType"] = ToMoonSharp<Game.SessionType, byte>();
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
