using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Data {
    public class PatchHelper {
        public static readonly string FeatureFullDay = "CONDITIONS_24H";
        public static readonly string FeatureSpecificDate = "CONDITIONS_SPECIFIC_DATE";
        public static readonly string FeatureCustomGForces = "VIEW_CUSTOM_GFORCES";
        public static readonly string FeatureExtraScreenshotFormats = "EXTRA_SCREENSHOT_FORMATS";
        public static readonly string FeatureNeedsOdometerValue = "NEEDS_ODOMETER_VALUE";
        public static readonly string FeatureDynamicShadowResolution = "DYNAMIC_SHADOWS_RESOLUTION";
        public static readonly string FeaturePovForButtons = "POV_FOR_BUTTONS";
        public static readonly string FeatureTrackDaySpeedLimit = "TRACK_DAY_SPEED_LIMIT";
        public static readonly string FeatureCustomBindingsJoystickModifiers = "CUSTOM_BINDINGS_JOYSTICK_MODIFIERS";
        public static readonly string FeatureKeyboardForcedThrottle = "KEYBOARD_FORCED_THROTTLE";

        public static bool OptionPatchSupport = true;

        public static readonly string MainFileName = "dwrite.dll";

        public static event EventHandler Reloaded;

        public static string GetMainFilename() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, MainFileName);
        }

        public static string GetRootDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, "extension");
        }

        public static string GetConfigFilename(string configName) {
            return Path.Combine(GetRootDirectory(), "config", configName);
        }

        public static string GetUserConfigFilename(string configName) {
            return Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension", configName);
        }

        public static string GetManifestFilename() {
            return GetConfigFilename("data_manifest.ini");
        }

        public static string GetInstalledLog() {
            return Path.Combine(GetRootDirectory(), "installed.log");
        }

        private static Dictionary<string, IniFile> _configs = new Dictionary<string, IniFile>();
        private static Dictionary<string, bool> _featureSupported = new Dictionary<string, bool>();

        [CanBeNull]
        private static string TryToRead(string filename) {
            try {
                if (File.Exists(filename)) {
                    return FileUtils.ReadAllText(filename);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
            return null;
        }

        public static IniFile GetConfig([NotNull] string configName) {
            return _configs.GetValueOrSet(configName, () => IniFile.Parse(
                    TryToRead(GetConfigFilename(configName))
                            + Environment.NewLine
                            + TryToRead(GetUserConfigFilename(configName))));
        }

        private class FeatureTester : ITester<object> {
            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(object obj, string key, ITestEntry value) {
                return value.Test(null);
            }
        }

        private static ITestEntry FeatureTestEntryFactory(string item) {
            var v = item.As<bool?>(null);
            if (v.HasValue) {
                return new ConstTestEntry(v.Value);
            }

            var split = item.Split(new[] { ':' }, 2);
            var config = GetConfig(split[0].Trim());
            if (split.Length == 1) {
                return new ConstTestEntry(config["BASIC"].GetBool("ENABLED", false));
            }

            var keyValue = split[1].Split(new[] { '=' }, 2);
            var sectionKey = keyValue[0].Split(new[] { '/' }, 2);
            return new ConstTestEntry(keyValue.Length == 1
                    ? config[sectionKey[0].Trim()].GetBool(sectionKey.Length == 2 ? sectionKey[1].Trim() : @"ENABLED", false)
                    : config[sectionKey[0].Trim()].GetPossiblyEmpty(sectionKey.Length == 2 ? sectionKey[1].Trim() : @"ENABLED") == keyValue[1].Trim());
        }

        public static bool IsFeatureSupported([CanBeNull] string featureId) {
            if (string.IsNullOrWhiteSpace(featureId)) return true;
            return _featureSupported.GetValueOrSet(featureId, () => {
                if (GetInstalledVersion() == null || !GetConfig("general.ini")["BASIC"].GetBool("ENABLED", true)) return false;
                var query = GetManifest()["FEATURES"].GetNonEmpty(featureId);
                if (string.IsNullOrWhiteSpace(query)) return false;
                var filter = Filter.Create(query, new FilterParams { CustomTestEntryFactory = FeatureTestEntryFactory });
                return filter.Test(new FeatureTester(), new object());
            });
        }

        public static int ClampTime(int time) {
            return IsFeatureSupported(FeatureFullDay)
                    ? time.Clamp(0, CommonAcConsts.TimeAbsoluteMaximum)
                    : time.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
        }

        private static Lazier<Tuple<string, string>> _installed;

        static PatchHelper() {
            _installed = Lazier.Create(() => {
                var installedLogFilename = GetInstalledLog();
                if (!File.Exists(installedLogFilename)) return Tuple.Create<string, string>(null, null);

                string version = null, build = null;
                foreach (var line in File.ReadAllLines(installedLogFilename).Select(x => x.Trim()).Where(x => !x.StartsWith(@"#"))) {
                    var keyValue = line.Split(new[] { ':' }, 2, StringSplitOptions.None);
                    if (keyValue.Length != 2) continue;
                    switch (keyValue[0].Trim()) {
                        case "version":
                            version = keyValue[1].Trim();
                            break;
                        case "build":
                            build = keyValue[1].Trim();
                            break;
                    }
                }

                return Tuple.Create(version, build);
            });
        }

        public static IniFile GetManifest() {
            return GetConfig("data_manifest.ini");
        }

        [CanBeNull]
        public static string GetInstalledVersion() {
            return GetManifest()["VERSION"].GetNonEmpty("SHADERS_PATCH");
        }

        [CanBeNull]
        public static string GetInstalledBuild() {
            return GetManifest()["VERSION"].GetNonEmpty("SHADERS_PATCH_BUILD");
        }

        public static void Reload() {
            _configs.Clear();
            _featureSupported.Clear();
            _installed.Reset();
            ActionExtension.InvokeInMainThreadAsync(() => Reloaded?.Invoke(null, EventArgs.Empty));
        }
    }
}