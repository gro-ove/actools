using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class WeatherProceduralHelper : WeatherSpecificHelperBase {
        public static bool Option24HourMode = false;

        protected override bool SetOverride(WeatherObject weather, IniFile file) {
            var directory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = AcRootDirectory.Instance.RequireValue;

            try {
                if (Option24HourMode) {
                    for (var i = 0; i < 24 * 60 * 60; i += 30 * 60) {
                        ProcessScripts(weather, file, i);
                    }
                }

                ProcessScripts(weather, file);

                if (_updateRoadTemperature) {
                    var section = new IniFile(weather.IniFilename)["LAUNCHER"];
                    if (section.ContainsKey(@"TEMPERATURE_COEFF")) {
                        file["TEMPERATURE"].Set("ROAD", Game.ConditionProperties.GetRoadTemperature(
                                Game.ConditionProperties.GetSeconds(file["LIGHTING"].GetDouble("SUN_ANGLE", 0d)),
                                file["TEMPERATURE"].GetDouble("AMBIENT", 20d),
                                section.GetDouble(@"TEMPERATURE_COEFF", 0d)), "F0");
                    }
                }
            } catch (ScriptRuntimeException e) {
                NonfatalError.NotifyBackground("Can’t run weather script", $"Exception at {e.DecoratedMessage}.", e);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t run weather script", e);
            } finally {
                Environment.CurrentDirectory = directory;
            }

            return false;
        }

        private static void ProcessScripts(WeatherObject weather, IniFile file, int? customTime = null) {
            if (customTime.HasValue) {
                file = file.Clone();
                file["LIGHTING"].Set("SUN_ANGLE", Game.ConditionProperties.GetSunAngle(customTime.Value));
            }

            WeatherProceduralContext context = null;
            WeatherProceduralContext GetContext() {
                return context ?? (context = new WeatherProceduralContext(file, weather.Location));
            }

            var destination = customTime.HasValue ? Path.Combine(weather.Location, @"__special", customTime.Value.ToString()) : null;
            ProcessFile(weather, GetContext, @"weather.ini", new[] { @"CLOUDS", @"FOG", @"CAR_LIGHTS", @"__CLOUDS_TEXTURES", @"__CUSTOM_LIGHTING" },
                    (table, ini) => {
                        var customClouds = table[@"CLOUDS_TEXTURES"];
                        if (customClouds != null) {
                            ini[@"__CLOUDS_TEXTURES"].Set("LIST", ToIniFileValue(customClouds));
                        }

                        var extraParams = table[@"EXTRA_PARAMS"];
                        if (extraParams != null) {
                            var s = new IniFileSection(null);
                            MapOutputSection(extraParams, s);
                            if (s.ContainsKey(@"TEMPERATURE_COEFF")) {
                                ini[@"LAUNCHER"].Set("TEMPERATURE_COEFF", s.GetNonEmpty("TEMPERATURE_COEFF"));
                            }
                            if (s.ContainsKey(@"DISABLE_SHADOWS")) {
                                ini[@"__LAUNCHER_CM"].Set("DISABLE_SHADOWS", s.GetNonEmpty("DISABLE_SHADOWS"));
                            }
                        }

                        MapOutputSection(table[@"LAUNCHER"], ini[@"LAUNCHER"], "TEMPERATURE_COEFF");
                        MapOutputSection(table[@"__LAUNCHER_CM"], ini[@"__LAUNCHER_CM"], "DISABLE_SHADOWS");
                        MapOutputSection(table[@"CUSTOM_LIGHTING"], ini[@"__CUSTOM_LIGHTING"]);
                    }, destination);
            ProcessFile(weather, GetContext, @"colorCurves.ini", new[] { @"HEADER", @"HORIZON", @"SKY", @"SUN", @"AMBIENT" }, null, destination);
            ProcessFile(weather, GetContext, @"filter.ini", new[] {
                @"YEBIS", @"OPTIMIZATIONS", @"VARIOUS", @"AUTO_EXPOSURE", @"GODRAYS", @"HEAT_SHIMMER", @"TONEMAPPING", @"DOF", @"CHROMATIC_ABERRATION",
                @"FEEDBACK", @"VIGNETTING", @"DIAPHRAGM", @"AIRYDISC", @"GLARE", @"LENSDISTORTION", @"ANTIALIAS", @"COLOR"
            }, null, destination);
            ProcessFile(weather, GetContext, @"tyre_smoke.ini", new[] { @"SETTINGS", @"TRIGGERS" }, null, destination);
            ProcessFile(weather, GetContext, @"tyre_smoke_grass.ini", new[] { @"SETTINGS" }, null, destination);
            ProcessFile(weather, GetContext, @"tyre_pieces_grass.ini", new[] { @"SETTINGS" }, null, destination);
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
            public LuaIniFile TyrePiecesGrassIni => _tyrePiecesGrassIni.Value;

            [UsedImplicitly]
            public LuaIniFile UserFilterIni => _userFilterIni.Value;

            [UsedImplicitly]
            public LuaIniFile TrackLightingIni => _trackLightingIni.Value;

            [UsedImplicitly]
            public LuaIniFile VideoIni => _videoIni.Value;

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

            private readonly Lazier<LuaIniFile> _weatherIni, _colorCurvesIni, _weatherFilterIni, _tyreSmokeIni, _tyreSmokeGrassIni, _tyrePiecesGrassIni,
                    _userFilterIni, _trackLightingIni, _videoIni;

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
                _tyrePiecesGrassIni = Lazier.Create(() => new LuaIniFile(Path.Combine(weatherLocation, "tyre_pieces_grass.ini")));
                _userFilterIni = Lazier.Create(() => new LuaIniFile(PpFiltersManager.Instance.GetByAcId(AcSettingsHolder.Video.PostProcessingFilter)?.Location));
                _videoIni = Lazier.Create(() => new LuaIniFile(AcSettingsHolder.Video.Filename));

                // BUG: USE BACKUPED VALUES IF ANY INSTEAD!
                _trackLightingIni = Lazier.Create(() => new LuaIniFile(Path.Combine(Track.DataDirectory, "lighting.ini")));

                _track = Lazier.Create(() => TracksManager.Instance.GetLayoutById(TrackId));
                _activeTrackSkins = Lazier.Create(() => Track.MainTrackObject.EnabledOnlySkins.Where(x => x.IsActive).ToList());
            }
        }

        private static bool _scriptRegistered;
        private readonly bool _updateRoadTemperature;

        public WeatherProceduralHelper(bool updateRoadTemperature) {
            _updateRoadTemperature = updateRoadTemperature;
        }

        private static void ProcessFile(WeatherObject weather, Func<WeatherProceduralContext> contextCallback, string fileName, string[] outputSections,
                Action<Table, IniFile> customFn = null, string destination = null) {
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

            try {
                state.DoString($@"math.randomseed({DateTime.Now.ToUnixTimestamp() % 10000})");
            } catch (Exception e) {
                Logging.Warning(e);
            }

            ((Table)state.Globals[@"strutils"])[@"tovec"] = (Func<object, object>)(i => {
                if (i is string s) {
                    return s.Split(',').Select(x => x.As(0d)).ToList();
                }
                return i;
            });

            state.Globals[@"input"] = contextCallback();
            state.DoFile(scriptLocation);

            var output = new IniFile(Path.Combine(destination ?? weather.Location, fileName));
            FileUtils.EnsureFileDirectoryExists(output.Filename);

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

        private static void MapOutputSection(object outputValue, IniFileSection resultSection, [Localizable(false)] params string[] keys) {
            switch (outputValue) {
                case IniFileSection e:
                    foreach (var v in e) {
                        if (!(keys?.Length > 0) || keys.Contains(v.Key)) {
                            resultSection.Set(v.Key, v.Value);
                        }
                    }
                    break;
                case LuaIniSection e:
                    foreach (var v in e.ToIniValues()) {
                        if (!(keys?.Length > 0) || keys.Contains(v.Key)) {
                            resultSection.Set(v.Key, v.Value);
                        }
                    }
                    break;
                case Table o:
                    foreach (var key in o.Keys) {
                        var keyString = key.CastToString();
                        if (!(keys?.Length > 0) || keys.Contains(keyString)) {
                            resultSection[keyString] = ToIniFileValue(o[key]);
                        }
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