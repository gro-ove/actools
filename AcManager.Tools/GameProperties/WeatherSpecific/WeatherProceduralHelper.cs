using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherProceduralHelper : WeatherSpecificHelperBase {
        public class LuaIniFile : IEnumerable<KeyValuePair<string, LuaIniSection>> {
            public LuaIniFile(string source) : this(new IniFile(source)) { }

            public LuaIniFile(IniFile source) {
                foreach (var pair in source) {
                    var section = this[pair.Key];
                    foreach (var value in pair.Value) {
                        section[value.Key] = ToLuaValue(value.Key, value.Value);
                    }
                }
            }

            private static object ToLuaValue(string key, string iniValue) {
                if (string.IsNullOrWhiteSpace(iniValue)) return null;

                var pieces = iniValue.Split(',');
                if (pieces.Length == 1) {
                    return pieces[0].Trim();
                }

                if (key == @"LIST") {
                    return pieces.ToList();
                }

                var numbers = new double[pieces.Length];
                for (var i = 0; i < pieces.Length; i++) {
                    var piece = pieces[i].Trim();
                    pieces[i] = piece;
                    numbers[i] = piece == string.Empty ? 0d : piece.As(double.NaN);
                }

                return new Vector(numbers);
            }

            private readonly Dictionary<string, LuaIniSection> _content = new Dictionary<string, LuaIniSection>();

            public Dictionary<string, LuaIniSection>.KeyCollection Keys => _content.Keys;

            public Dictionary<string, LuaIniSection>.ValueCollection Values => _content.Values;

            [NotNull]
            public LuaIniSection this[[NotNull, LocalizationRequired(false)] string key] {
                get {
                    if (_content.TryGetValue(key, out var result)) return result;
                    result = new LuaIniSection();
                    _content[key] = result;
                    return result;
                }
            }

            public void Clear() {
                _content.Clear();
            }

            public int Count => _content.Count;

            public IEnumerator<KeyValuePair<string, LuaIniSection>> GetEnumerator() {
                return _content.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public class Vector : IEnumerable<double> {
            private readonly List<double> _list;

            public Vector() {
                _list = new List<double>();
            }

            public Vector([NotNull] IEnumerable<double> collection) {
                _list = new List<double>(collection);
            }

            public double X => this[1];
            public double Y => this[2];
            public double Z => this[3];
            public double W => this[4];
            public double R => this[1];
            public double G => this[2];
            public double B => this[3];
            public double A => this[4];

            // ReSharper disable InconsistentNaming
            public double x => this[1];
            public double y => this[2];
            public double z => this[3];
            public double w => this[4];
            public double r => this[1];
            public double g => this[2];
            public double b => this[3];
            public double a => this[4];
            // ReSharper restore InconsistentNaming

            public double this[int key] {
                get => key >= 1 && key <= _list.Count ? _list[key - 1] : 0d;
                set {
                    if (key < 1) return;
                    if (key <= _list.Count) {
                        _list[key - 1] = value;
                    } else {
                        while (_list.Count < key - 1) {
                            _list.Add(0d);
                        }
                        _list.Add(value);
                    }
                }
            }

            public void Clear() {
                _list.Clear();
            }

            public int Count => _list.Count;

            public static Vector operator ++(Vector a) {
                return a + 1;
            }

            public static Vector operator --(Vector a) {
                return a - 1;
            }

            public static Vector operator +(Vector a, double b) {
                return new Vector(a.Select(x => x + b));
            }

            public static Vector operator +(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x + y));
            }

            public static Vector operator -(Vector a, double b) {
                return new Vector(a.Select(x => x - b));
            }

            public static Vector operator -(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x - y));
            }

            public static Vector operator *(Vector a, double b) {
                return new Vector(a.Select(x => x * b));
            }

            public static Vector operator *(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x * y));
            }

            public static Vector operator /(Vector a, double b) {
                return new Vector(a.Select(x => x / b));
            }

            public static Vector operator /(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x / y));
            }

            public static Vector operator %(Vector a, double b) {
                return new Vector(a.Select(x => x % b));
            }

            public static Vector operator %(Vector a, Vector b) {
                return new Vector(a.Zip(b, (x, y) => x % y));
            }

            public static bool operator ==(Vector a, double b) {
                return a?.All(x => x == b) == true;
            }

            public static bool operator !=(Vector a, double b) {
                return !(a == b);
            }

            public static bool operator ==(double b, Vector a) {
                return b == a;
            }

            public static bool operator !=(double b, Vector a) {
                return !(b == a);
            }

            public static bool operator <(Vector a, double b) {
                return a?.All(x => x < b) == true;
            }

            public static bool operator >(Vector a, double b) {
                return a?.All(x => x > b) == true;
            }

            public static bool operator <=(Vector a, double b) {
                return a?.All(x => x <= b) == true;
            }

            public static bool operator >=(Vector a, double b) {
                return a?.All(x => x >= b) == true;
            }

            public static bool operator <(double b, Vector a) {
                return a < b;
            }

            public static bool operator >(double b, Vector a) {
                return a > b;
            }

            public static bool operator <=(double b, Vector a) {
                return a <= b;
            }

            public static bool operator >=(double b, Vector a) {
                return a >= b;
            }

            public static bool operator ==(Vector a, Vector b) {
                return a == b || a?.Equals(b) == true;
            }

            public static bool operator !=(Vector a, Vector b) {
                return !(a == b);
            }

            protected bool Equals(Vector other) {
                return this.SequenceEqual(other);
            }

            public IEnumerator<double> GetEnumerator() {
                return _list.GetEnumerator();
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GetType() == GetType() && Equals((Vector)obj);
            }

            public override int GetHashCode() {
                return this.GetEnumerableHashCode();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public class LuaIniSection : Dictionary<string, object> {
            public IEnumerable<KeyValuePair<string, string>> ToIniValues() {
                return this.Select(x => new KeyValuePair<string, string>(x.Key, ToIniFileValue(x.Value)));
            }
        }

        protected override bool SetOverride(WeatherObject weather, IniFile file) {
            WeatherProceduralContext context = null;

            WeatherProceduralContext GetContext() {
                return context ?? (context = new WeatherProceduralContext(file, weather.Location));
            }

            try {
                ProcessFile(weather, GetContext, @"weather.ini", new[] { @"CLOUDS", @"FOG", @"CAR_LIGHTS", @"__CLOUDS_TEXTURES", @"__CUSTOM_LIGHTING" },
                        (table, ini) => {
                            var customClouds = table[@"CLOUDS_TEXTURES"];
                            if (customClouds != null) {
                                ini[@"__CLOUDS_TEXTURES"].Set("LIST", ToIniFileValue(customClouds));
                            }

                            MapOutputSection(table[@"CUSTOM_LIGHTING"], ini[@"__CUSTOM_LIGHTING"]);
                        });
                ProcessFile(weather, GetContext, @"colorCurves.ini", new[] { @"HEADER", @"HORIZON", @"SKY", @"SUN", @"AMBIENT" });
                ProcessFile(weather, GetContext, @"filter.ini", new[] {
                    @"YEBIS", @"OPTIMIZATIONS", @"VARIOUS", @"AUTO_EXPOSURE", @"GODRAYS", @"HEAT_SHIMMER", @"TONEMAPPING", @"DOF", @"CHROMATIC_ABERRATION",
                    @"FEEDBACK", @"VIGNETTING", @"DIAPHRAGM", @"AIRYDISC", @"GLARE", @"LENSDISTORTION", @"ANTIALIAS", @"COLOR"
                });
                ProcessFile(weather, GetContext, @"tyre_smoke.ini", new[] { @"SETTINGS", @"TRIGGERS" });
                ProcessFile(weather, GetContext, @"tyre_smoke_grass.ini", new[] { @"SETTINGS" });
            } catch (ScriptRuntimeException e) {
                NonfatalError.NotifyBackground("Can’t run weather script", $"Exception at {e.DecoratedMessage}.", e);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t run weather script", e);
            }

            return false;
        }

        public class WeatherProceduralContext {
            private readonly IniFile _raceIni;

            [UsedImplicitly]
            public LuaIniFile RaceIni;

            [UsedImplicitly]
            public LuaIniFile WeatherIni => _weatherIni.Value;

            [UsedImplicitly]
            public LuaIniFile ColorCurvesIni => _colorCurvesIni.Value;

            [UsedImplicitly]
            public LuaIniFile WeatherFilterIni => _weatherFilterIni.Value;

            [UsedImplicitly]
            public LuaIniFile TyreSmokeIni => _tyreSmokeIni.Value;

            [UsedImplicitly]
            public LuaIniFile TyreSmokeGrassIni => _tyreSmokeGrassIni.Value;

            [UsedImplicitly]
            public LuaIniFile UserFilterIni => _userFilterIni.Value;

            [UsedImplicitly]
            public LuaIniFile TrackLightingIni => _trackLightingIni.Value;

            [UsedImplicitly]
            public string TrackId;

            [UsedImplicitly]
            public TrackObjectBase Track => _track.Value;

            [UsedImplicitly]
            public double Seconds => Game.ConditionProperties.GetSeconds(_raceIni["LIGHTING"].GetDouble("SUN_ANGLE", 0));

            [UsedImplicitly]
            public double Minutes => Seconds / 60d;

            [UsedImplicitly]
            public double Hours => Minutes / 60d;

            [UsedImplicitly]
            public List<TrackSkinObject> ActiveTrackSkins => _activeTrackSkins.Value;

            [UsedImplicitly]
            public string TrackSeason => ActiveTrackSkins.FirstOrDefault(x => x.Categories.Contains(@"season"))?.DisplayName.ToLowerInvariant();

            private readonly Lazier<LuaIniFile> _weatherIni, _colorCurvesIni, _weatherFilterIni, _tyreSmokeIni, _tyreSmokeGrassIni, _userFilterIni,
                    _trackLightingIni;

            private readonly Lazier<TrackObjectBase> _track;
            private readonly Lazier<List<TrackSkinObject>> _activeTrackSkins;

            public WeatherProceduralContext(IniFile raceIni, string weatherLocation) {
                _raceIni = raceIni;
                RaceIni = new LuaIniFile(raceIni);
                TrackId = $@"{raceIni["RACE"].GetNonEmpty("TRACK")}/{raceIni["RACE"].GetNonEmpty("CONFIG_TRACK")}".TrimEnd('/');
                _weatherIni = Lazier.Create(() => new LuaIniFile(Path.Combine(weatherLocation, "weather.ini")));
                _colorCurvesIni = Lazier.Create(() => new LuaIniFile(Path.Combine(weatherLocation, "colorCurves.ini")));
                _weatherFilterIni = Lazier.Create(() => new LuaIniFile(Path.Combine(weatherLocation, "filter.ini")));
                _tyreSmokeIni = Lazier.Create(() => new LuaIniFile(Path.Combine(weatherLocation, "tyre_smoke.ini")));
                _tyreSmokeGrassIni = Lazier.Create(() => new LuaIniFile(Path.Combine(weatherLocation, "tyre_smoke_grass.ini")));
                _userFilterIni = Lazier.Create(() => new LuaIniFile(PpFiltersManager.Instance.GetByAcId(AcSettingsHolder.Video.PostProcessingFilter)?.Location));

                // BUG: USE BACKUPED VALUES IF ANY INSTEAD!
                _trackLightingIni = Lazier.Create(() => new LuaIniFile(Path.Combine(Track.DataDirectory, "lighting.ini")));

                _track = Lazier.Create(() => TracksManager.Instance.GetLayoutById(TrackId));
                _activeTrackSkins = Lazier.Create(() => Track.MainTrackObject.EnabledOnlySkins.Where(x => x.IsActive).ToList());
            }
        }

        private static bool _scriptRegistered;

        private static void ProcessFile(WeatherObject weather, Func<WeatherProceduralContext> contextCallback, string fileName, string[] outputSections,
                Action<Table, IniFile> customFn = null) {
            var scriptLocation = Path.Combine(weather.Location, Path.GetFileNameWithoutExtension(fileName) + @".lua");
            if (!File.Exists(scriptLocation)) return;

            if (!_scriptRegistered) {
                _scriptRegistered = true;
                UserData.RegisterType<Vector>();
                UserData.RegisterType<IniFile>();
                UserData.RegisterType<IniFileSection>();
                UserData.RegisterType<LuaIniFile>();
                UserData.RegisterType<LuaIniSection>();
                UserData.RegisterType<WeatherProceduralContext>();
            }

            var state = LuaHelper.GetExtended();
            if (state == null) {
                NonfatalError.NotifyBackground("Can’t run weather script", "Lua interpreter failed to initialize.");
                return;
            }

            ((Table)state.Globals[@"strutils"])[@"tovec"] = (Func<object, object>)(i => {
                if (i is string s) {
                    return s.Split(',').Select(x => x.As(0d)).ToList();
                }
                return i;
            });

            state.Globals[@"input"] = contextCallback();
            state.DoFile(scriptLocation);

            var output = new IniFile(Path.Combine(weather.Location, fileName));

            switch (state.Globals[@"copyValuesFrom"]) {
                case IniFile file:
                    foreach (var e in file) {
                        var s = output[e.Key];
                        foreach (var v in e.Value) {
                            s.Set(v.Key, v.Value);
                        }
                    }
                    break;
                case LuaIniFile luaFile:
                    foreach (var e in luaFile) {
                        var s = output[e.Key];
                        foreach (var v in e.Value.ToIniValues()) {
                            s.Set(v.Key, v.Value);
                        }
                    }
                    break;
            }

            foreach (var section in outputSections) {
                MapOutputSection(state.Globals[section], output[section]);
            }

            customFn?.Invoke(state.Globals, output);
            output.Save();
        }

        private static void MapOutputSection(object outputValue, IniFileSection resultSection) {
            switch (outputValue) {
                case IniFileSection e:
                    foreach (var v in e) {
                        resultSection.Set(v.Key, v.Value);
                    }
                    break;
                case LuaIniSection e:
                    foreach (var v in e.ToIniValues()) {
                        resultSection.Set(v.Key, v.Value);
                    }
                    break;
                case Table o:
                    foreach (var key in o.Keys) {
                        resultSection[key.CastToString()] = ToIniFileValue(o[key]);
                    }
                    break;
            }
        }

        private static string ToIniFileValue(object v) {
            switch (v) {
                case Table t:
                    return t.Values.Select(x => x.CastToString()).JoinToString(@",");
                case IEnumerable<double> e:
                    return e.JoinToString(@",");
                case IEnumerable<string> e:
                    return e.JoinToString(@",");
                case null:
                    return null;
                default:
                    return v.ToString();
            }
        }

        protected override void DisposeOverride() { }
    }
}