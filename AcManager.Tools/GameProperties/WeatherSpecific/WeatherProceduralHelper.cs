using System;
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
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherProceduralHelper : WeatherSpecificHelperBase {
        protected override bool SetOverride(WeatherObject weather, IniFile file) {
            WeatherProceduralContext context = null;

            WeatherProceduralContext GetContext() {
                return context ?? (context = new WeatherProceduralContext(file, weather.Location));
            }

            try {
                ProcessFile(weather, GetContext, @"weather.ini", new[] { @"CLOUDS", @"FOG" });
                ProcessFile(weather, GetContext, @"colorCurves.ini", new[] { @"HEADER", @"HORIZON", @"SKY", @"SUN", @"AMBIENT" });
                ProcessFile(weather, GetContext, @"filter.ini", new[] {
                    @"YEBIS", @"OPTIMIZATIONS", @"VARIOUS", @"AUTO_EXPOSURE", @"GODRAYS", @"HEAT_SHIMMER", @"TONEMAPPING", @"DOF", @"CHROMATIC_ABERRATION",
                    @"FEEDBACK", @"VIGNETTING", @"DIAPHRAGM", @"AIRYDISC", @"GLARE", @"LENSDISTORTION", @"ANTIALIAS", @"COLOR"
                });
                ProcessFile(weather, GetContext, @"tyre_smoke.ini", new[] { @"SETTINGS", @"TRIGGERS" });
                ProcessFile(weather, GetContext, @"tyre_smoke_grass.ini", new[] { @"SETTINGS" });
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t run weather script", e);
            }

            return false;
        }

        public class WeatherProceduralContext {
            [UsedImplicitly]
            public IniFile RaceIni;

            [UsedImplicitly]
            public IniFile WeatherIni => _weatherIni.Value;

            [UsedImplicitly]
            public IniFile ColorCurvesIni => _colorCurvesIni.Value;

            [UsedImplicitly]
            public IniFile WeatherFilterIni => _weatherFilterIni.Value;

            [UsedImplicitly]
            public IniFile TyreSmokeIni => _tyreSmokeIni.Value;

            [UsedImplicitly]
            public IniFile TyreSmokeGrassIni => _tyreSmokeGrassIni.Value;

            [UsedImplicitly]
            public IniFile UserFilterIni => _userFilterIni.Value;

            [UsedImplicitly]
            public string TrackId;

            [UsedImplicitly]
            public TrackObjectBase Track => _track.Value;

            [UsedImplicitly]
            public double Seconds => Game.ConditionProperties.GetSeconds(RaceIni["LIGHTING"].GetDouble("SUN_ANGLE", 0));

            [UsedImplicitly]
            public double Minutes => Seconds / 60d;

            [UsedImplicitly]
            public double Hours => Minutes / 60d;

            [UsedImplicitly]
            public List<TrackSkinObject> ActiveTrackSkins => _activeTrackSkins.Value;

            [UsedImplicitly]
            public string TrackSeason => ActiveTrackSkins.FirstOrDefault(x => x.Categories.Contains(@"season"))?.DisplayName.ToLowerInvariant();

            private readonly Lazier<IniFile> _weatherIni, _colorCurvesIni, _weatherFilterIni, _tyreSmokeIni, _tyreSmokeGrassIni, _userFilterIni;
            private readonly Lazier<TrackObjectBase> _track;
            private readonly Lazier<List<TrackSkinObject>> _activeTrackSkins;

            public WeatherProceduralContext(IniFile raceIni, string weatherLocation) {
                RaceIni = raceIni;
                TrackId = $@"{raceIni["RACE"].GetNonEmpty("TRACK")}/{raceIni["RACE"].GetNonEmpty("CONFIG_TRACK")}".TrimEnd('/');
                _weatherIni = Lazier.Create(() => new IniFile(Path.Combine(weatherLocation, "weather.ini")));
                _colorCurvesIni = Lazier.Create(() => new IniFile(Path.Combine(weatherLocation, "colorCurves.ini")));
                _weatherFilterIni = Lazier.Create(() => new IniFile(Path.Combine(weatherLocation, "filter.ini")));
                _tyreSmokeIni = Lazier.Create(() => new IniFile(Path.Combine(weatherLocation, "tyre_smoke.ini")));
                _tyreSmokeGrassIni = Lazier.Create(() => new IniFile(Path.Combine(weatherLocation, "tyre_smoke_grass.ini")));
                _userFilterIni = Lazier.Create(() => new IniFile(PpFiltersManager.Instance.GetByAcId(AcSettingsHolder.Video.PostProcessingFilter)?.Location));
                _track = Lazier.Create(() => TracksManager.Instance.GetLayoutById(TrackId));
                _activeTrackSkins = Lazier.Create(() => Track.MainTrackObject.EnabledOnlySkins.Where(x => x.IsActive).ToList());
            }
        }

        private static bool _scriptRegistered;

        private static void ProcessFile(WeatherObject weather, Func<WeatherProceduralContext> contextCallback, string fileName, string[] outputSections) {
            var scriptLocation = Path.Combine(weather.Location, Path.GetFileNameWithoutExtension(fileName) + @".lua");
            if (!File.Exists(scriptLocation)) return;

            if (!_scriptRegistered) {
                _scriptRegistered = true;
                UserData.RegisterType<IniFile>();
                UserData.RegisterType<IniFileSection>();
                UserData.RegisterType<WeatherProceduralContext>();
            }

            var state = LuaHelper.GetExtended();
            if (state == null) {
                NonfatalError.NotifyBackground("Can’t run weather script", "Lua interpreter failed to initialize.");
                return;
            }

            state.Globals[@"input"] = contextCallback();
            state.DoFile(scriptLocation);

            var output = new IniFile(Path.Combine(weather.Location, fileName));
            foreach (var section in outputSections) {
                var s = output[section];
                if (state.Globals[section] is Table o) {
                    foreach (var key in o.Keys) {
                        var v = o[key];
                        if (v is Table t) {
                            s[key.CastToString()] = t.Values.Select(x => x.CastToString()).JoinToString(@",");
                        } else if (v != null) {
                            s[key.CastToString()] = v.ToString();
                        }
                    }
                }
            }

            output.Save();
        }

        protected override void DisposeOverride() { }
    }
}