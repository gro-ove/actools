using System;
using System.IO;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherSpecificVideoSettingsHelper : WeatherSpecificHelperBase {
        private const string FilterId = "__cm_weather";

        private static string Destination => Path.Combine(AcPaths.GetPpFiltersDirectory(AcRootDirectory.Instance.RequireValue), $"{FilterId}.ini");

        public static void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            try {
                var destination = Destination;
                if (File.Exists(destination)) {
                    File.Delete(destination);
                }

                {
                    var changed = false;
                    var ini = new IniFile(AcPaths.GetCfgVideoFilename());

                    var postProcess = ini["POST_PROCESS"];
                    if (postProcess.GetNonEmpty("FILTER") == FilterId) {
                        postProcess.Set("FILTER", postProcess.GetNonEmpty("__CM_PREWEATHER_ORIGINAL_FILTER") ?? @"default");
                        postProcess.Remove(@"__CM_PREWEATHER_ORIGINAL_FILTER");
                        changed = true;
                    }

                    var video = ini["VIDEO"];
                    if (video.ContainsKey(@"__CM_PREWEATHER_ORIGINAL_SHADOW_MAP_SIZE")) {
                        video.Set("SHADOW_MAP_SIZE", video.GetNonEmpty("__CM_PREWEATHER_ORIGINAL_SHADOW_MAP_SIZE"));
                        video.Remove(@"__CM_PREWEATHER_ORIGINAL_SHADOW_MAP_SIZE");
                        video.Remove(@"__CM_ORIGINAL_SHADOW_MAP_SIZE");
                        changed = true;
                    }

                    if (changed) {
                        ini.Save();
                    }
                }

                {
                    var changed = false;
                    var ini = new IniFile(AcSettingsHolder.Graphics.Filename);

                    var section = ini["DX11"];
                    if (section.ContainsKey(@"__CM_PREWEATHER_ORIGINAL_SKYBOX_REFLECTION_GAIN")) {
                        section.Set("SKYBOX_REFLECTION_GAIN", section.GetNonEmpty("__CM_PREWEATHER_ORIGINAL_SKYBOX_REFLECTION_GAIN"));
                        section.Remove(@"__CM_PREWEATHER_ORIGINAL_SKYBOX_REFLECTION_GAIN");
                        changed = true;
                    }

                    if (changed) {
                        ini.Save();
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private string _destination;

        protected override bool SetOverride(WeatherObject weather, IniFile file) {
            var videoCfg = AcPaths.GetCfgVideoFilename();
            if (!File.Exists(videoCfg)) return false;

            var customFilter = false;
            if (SettingsHolder.Drive.WeatherSpecificPpFilter) {
                var replacement = Path.Combine(weather.Location, "filter.ini");
                if (File.Exists(replacement)) {
                    customFilter = true;

                    _destination = Destination;
                    if (File.Exists(_destination)) {
                        File.Delete(_destination);
                    }

                    FileUtils.HardLinkOrCopy(replacement, _destination);
                }
            }

            var disableShadows = new IniFile(weather.IniFilename)["__LAUNCHER_CM"].GetBool("DISABLE_SHADOWS", false);
            var skyboxReflectionGain = new IniFile(weather.IniFilename)["__LAUNCHER_CM"].GetDoubleNullable("SKYBOX_REFLECTION_GAIN");

            if (!customFilter && !disableShadows && !skyboxReflectionGain.HasValue) {
                return false;
            }

            if (skyboxReflectionGain.HasValue) {
                var ini = new IniFile(AcSettingsHolder.Graphics.Filename);
                var section = ini["DX11"];
                section.Set("__CM_PREWEATHER_ORIGINAL_SKYBOX_REFLECTION_GAIN",
                        section.GetNonEmpty("__CM_ORIGINAL_SKYBOX_REFLECTION_GAIN") ?? section.GetNonEmpty("SKYBOX_REFLECTION_GAIN"));
                section.Set("SKYBOX_REFLECTION_GAIN", skyboxReflectionGain.Value);
                ini.Save();
            }

            if (customFilter || disableShadows) {
                var ini = new IniFile(videoCfg);

                if (customFilter) {
                    var section = ini["POST_PROCESS"];
                    section.Set("__CM_PREWEATHER_ORIGINAL_FILTER", section.GetNonEmpty("FILTER"));
                    section.Set("FILTER", FilterId);
                }

                if (disableShadows) {
                    var section = ini["VIDEO"];
                    section.Set("__CM_PREWEATHER_ORIGINAL_SHADOW_MAP_SIZE",
                            section.GetNonEmpty("__CM_ORIGINAL_SHADOW_MAP_SIZE") ?? section.GetNonEmpty("SHADOW_MAP_SIZE"));
                    section.Set("SHADOW_MAP_SIZE", -1);
                }

                ini.Save();
            }

            return true;
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}