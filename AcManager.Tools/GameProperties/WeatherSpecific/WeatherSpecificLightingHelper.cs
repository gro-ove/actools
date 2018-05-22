using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherSpecificLightingHelper : WeatherSpecificHelperBase {
        private static readonly StoredValue<string> LastModified = Stored.Get<string>("/LastModifiedLightingIni");
        private static readonly string[] Keys = { @"SUN_PITCH_ANGLE", @"SUN_HEADING_ANGLE" };

        public static void Revert() {
            var lastModified = LastModified.Value;
            if (lastModified == null) return;

            LastModified.Value = null;
            Process(lastModified, null);
        }

        private static bool Process(string lightingIniFilename, [CanBeNull] IniFileSection replacementSection) {
            var lightingIni = new IniFile(lightingIniFilename);
            var lightingSection = lightingIni["LIGHTING"];
            var changed = false;

            foreach (var key in Keys) {
                var backupKey = @"__CM_BACKUP_" + key;
                var replacementValue = replacementSection?.GetNonEmpty(key);
                Logging.Debug($"{key}: {replacementValue}");

                if (replacementValue != null) {
                    if (!lightingSection.ContainsKey(backupKey)) {
                        lightingSection.Set(backupKey, lightingSection.GetPossiblyEmpty(key));
                    }

                    lightingSection.Set(key, replacementValue);
                    LastModified.Value = lightingIni.Filename;
                    changed = true;
                } else if (lightingSection.ContainsKey(backupKey)) {
                    lightingSection.Set(key, lightingSection.GetPossiblyEmpty(backupKey));
                    lightingSection.Remove(backupKey);
                    changed = true;
                }
            }

            if (changed) {
                lightingIni.Save();
            }

            return changed;
        }

        protected override bool SetOverride(WeatherObject weather, IniFile raceIni) {
            var section = new IniFile(weather.IniFilename)["__CUSTOM_LIGHTING"];
            if (section.ContainsKey(@"SUN_ANGLE")) {
                raceIni[@"LIGHTING"].Set(@"SUN_ANGLE", section.GetPossiblyEmpty(@"SUN_ANGLE"));
            }

            var trackId = $@"{raceIni["RACE"].GetNonEmpty("TRACK")}/{raceIni["RACE"].GetNonEmpty("CONFIG_TRACK")}".TrimEnd('/');
            var track = TracksManager.Instance.GetLayoutById(trackId);
            return track != null && Process(Path.Combine(track.DataDirectory, "lighting.ini"), section);
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}