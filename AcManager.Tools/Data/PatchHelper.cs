﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcTools;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Data {
    public class PatchHelper {
        public static int MinimumTestOnlineVersion { get; } = 1061;

        public static int NonExistentVersion { get; } = 999999;

        public static readonly string FeatureTestOnline = "CSP_TEST_ONLINE";
        public static readonly string FeatureFullDay = "CONDITIONS_24H";
        public static readonly string FeatureSpecificDate = "CONDITIONS_SPECIFIC_DATE";
        public static readonly string FeatureCustomGForces = "VIEW_CUSTOM_GFORCES";
        public static readonly string FeatureExtraScreenshotFormats = "EXTRA_SCREENSHOT_FORMATS";
        public static readonly string FeatureNeedsOdometerValue = "NEEDS_ODOMETER_VALUE";
        public static readonly string FeatureDynamicShadowResolution = "DYNAMIC_SHADOWS_RESOLUTION";
        public static readonly string FeaturePovForButtons = "POV_FOR_BUTTONS";
        public static readonly string FeatureTrackDaySpeedLimit = "TRACK_DAY_SPEED_LIMIT";
        public static readonly string FeatureCustomBindingsJoypadModifiers = "CUSTOM_BINDINGS_JOYSTICK_MODIFIERS";
        public static readonly string FeatureKeyboardForcedThrottle = "KEYBOARD_FORCED_THROTTLE";
        public static readonly string FeatureSharedMemoryReduceGForcesWhenSlow = "SHARED_MEMORY_REDUCE_GFORCES_WHEN_SLOW";
        public static readonly string FeatureWindowPosition = "WINDOW_POSITION";
        public static readonly string FeatureReplayFullPath = "REPLAY_FULL_PATH";
        public static readonly string FeatureCustomRenderingModes = "CUSTOM_RENDERING_MODES";
        public static readonly string FeatureJoypadIndexAware = "JOYPAD_INDEX_AWARE";

        public class AudioDescription : Displayable {
            public string Id { get; set; }
            public double DefaultLevel { get; set; }
        }

        public static bool OptionPatchSupport = true;

        public static readonly string MainFileName = "dwrite.dll";

        public static event EventHandler Reloaded;
        public static event EventHandler FeaturesInvalidated;

        [CanBeNull]
        public static string TryGetMainFilename() {
            return AcRootDirectory.Instance.Value == null ? null : Path.Combine(AcRootDirectory.Instance.Value, MainFileName);
        }

        [CanBeNull]
        public static string TryGetRootDirectory() {
            return AcRootDirectory.Instance.Value == null ? null : Path.Combine(AcRootDirectory.Instance.Value, "extension");
        }

        [NotNull]
        public static string RequireRootDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, "extension");
        }

        [CanBeNull]
        public static string TryGetConfigFilename(string configName) {
            var directory = TryGetRootDirectory();
            return directory == null ? null : Path.Combine(directory, "config", configName);
        }

        public static string GetUserConfigFilename(string configName) {
            return Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension", configName);
        }

        [CanBeNull]
        public static string TryGetManifestFilename() {
            return TryGetConfigFilename("data_manifest.ini");
        }

        [CanBeNull]
        public static string TryGetInstalledLog() {
            var directory = TryGetRootDirectory();
            return directory == null ? null : Path.Combine(directory, "installed.log");
        }

        private static List<SettingEntry> _customVideoModes;

        public static IEnumerable<SettingEntry> CustomVideoModes {
            get {
                if (!IsActive() || !IsFeatureSupported(FeatureCustomRenderingModes)) {
                    return new SettingEntry[0];
                }

                if (_customVideoModes == null) {
                    _customVideoModes = GetManifest()?["CUSTOM_RENDER_MODES"]
                            .Select(x => new { x, p = x.Value.Split(',').AlterArray(y => y.Trim()) })
                            .Where(x => x.p.Length < 2 || TestQuery(x.p[1]))
                            .Select(x => new SettingEntry(x.x.Key, x.p[0])).ToList() ?? new List<SettingEntry>();
                }
                return _customVideoModes;
            }
        }

        private static Dictionary<string, IniFile> _configs = new Dictionary<string, IniFile>();
        private static Dictionary<string, bool> _featureSupported = new Dictionary<string, bool>();
        private static BetterObservableCollection<AudioDescription> _audioDescriptions = new BetterObservableCollection<AudioDescription>();
        private static bool _audioDescriptionsSet;
        private static bool? _active;

        [CanBeNull]
        private static string TryToRead([CanBeNull] string filename) {
            if (filename == null) return null;
            try {
                if (File.Exists(filename)) {
                    return FileUtils.ReadAllText(filename);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
            return null;
        }

        [CanBeNull]
        public static IniFile TryGetConfig([NotNull] string configName) {
            return _configs.GetValueOrSet(configName, () => IniFile.Parse(
                    TryToRead(TryGetConfigFilename(configName))
                            + Environment.NewLine
                            + TryToRead(GetUserConfigFilename(configName))));
        }

        [CanBeNull]
        public static string GetActualConfigValue([NotNull] string configName, string section, string key) {
            var instance = PatchSettingsModel.GetExistingInstance();
            if (instance != null) {
                var config = instance.Configs?.FirstOrDefault(x => x.Filename.EndsWith(configName));
                return config?.Sections.GetByIdOrDefault(section)?.GetByIdOrDefault(key)?.Value;
            }

            return TryGetConfig(configName)?[section].GetNonEmpty(key);
        }

        private class FeatureTester : ITester<object> {
            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(object obj, string key, ITestEntry value) {
                return value.Test(null);
            }
        }

        private static List<string> _monitoredValues = new List<string>();
        private static Busy _featuresInvalidatedBusy = new Busy();

        private static void AddMonitored(string configName, string section, string key) {
            var keyFull = $@"{configName}/{section}/{key}".ToLowerInvariant();
            if (!_monitoredValues.Contains(keyFull)) {
                _monitoredValues.Add(keyFull);
            }
        }

        public static void InvalidateFeatures() {
                _featuresInvalidatedBusy.Delay(() => {
                    _customVideoModes = null;
                    FeaturesInvalidated?.Invoke(null, EventArgs.Empty);
                }, 100);

        }

        public static void OnConfigPropertyChanged(string configName, string section, string key) {
            var keyFull = $@"{configName}/{section}/{key}".ToLowerInvariant();
            if (_monitoredValues.Contains(keyFull)) {
                InvalidateFeatures();
            }
        }

        private static ITestEntry FeatureTestEntryFactory(string item) {
            var v = item.As<bool?>();
            if (v.HasValue) {
                return new ConstTestEntry(v.Value);
            }

            var split = item.Split(new[] { ':' }, 2);
            string sectionKey;
            string valueKey;
            string targetValue = null;
            if (split.Length == 1) {
                sectionKey = "BASIC";
                valueKey = "ENABLED";
            } else {
                var keyValueJoined = split[1].Split(new[] { '=' }, 2);
                var sectionKeyJoined = keyValueJoined[0].Split(new[] { '/' }, 2);
                sectionKey = sectionKeyJoined[0].Trim();
                valueKey = sectionKeyJoined.Length == 2 ? sectionKeyJoined[1].Trim() : @"ENABLED";
                targetValue = keyValueJoined.Length == 1 ? null : keyValueJoined[1].Trim();
            }

            AddMonitored(split[0].Trim(), sectionKey, valueKey);
            var value = GetActualConfigValue(split[0].Trim(), sectionKey, valueKey);
            return new ConstTestEntry(targetValue == null
                    ? !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, @"^(?:0|off|false|no|none|disabled)$", RegexOptions.IgnoreCase)
                    : value == targetValue);
        }

        public static string GetWindowPositionConfig() {
            return Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension", "window_position.ini");
        }

        public static bool IsActive() {
            return (_active ?? (_active = GetActualConfigValue("general.ini", "BASIC", "ENABLED").As(false))).Value;
        }

        private static bool TestQuery(string query, bool emptyFallback = false) {
            if (string.IsNullOrWhiteSpace(query)) return emptyFallback;
            var filter = Filter.Create(query, new FilterParams { CustomTestEntryFactory = FeatureTestEntryFactory });
            return filter.Test(new FeatureTester(), new object());
        }

        public static bool IsFeatureSupported([CanBeNull] string featureId) {
            if (string.IsNullOrWhiteSpace(featureId)) return true;
            return _featureSupported.GetValueOrSet(featureId, () => {
                if (GetInstalledVersion() == null || !IsActive()) return false;
                if (featureId == FeatureWindowPosition) return true;
                var query = GetManifest()?["FEATURES"].GetNonEmpty(featureId);
                return TestQuery(query);
            });
        }

        public static IReadOnlyList<AudioDescription> GetExtraAudioLevels() {
            if (!_audioDescriptionsSet) {
                _audioDescriptionsSet = true;
                if (IsActive()) {
                    _audioDescriptions.ReplaceEverythingBy_Direct(GetManifest()?["AUDIO_LEVELS"].Select(p => {
                        if (p.Key.EndsWith("_")) return null;

                        var value = p.Value.WrapQuoted(out var unwrap)
                                .Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                        if (value.Count != 2) return null;

                        return new AudioDescription {
                            Id = p.Key,
                            DefaultLevel = unwrap(value[1]).As(0.8),
                            DisplayName = unwrap(value[0])
                        };
                    }).NonNull() ?? new AudioDescription[0]);
                } else {
                    _audioDescriptions.Clear();
                }
            }
            return _audioDescriptions;
        }

        public static int ClampTime(int time) {
            return IsFeatureSupported(FeatureFullDay)
                    ? time.Clamp(0, CommonAcConsts.TimeAbsoluteMaximum)
                    : time.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
        }

        private static Lazier<Tuple<string, string>> _installed;

        static PatchHelper() {
            _installed = Lazier.Create(() => {
                var installedLogFilename = TryGetInstalledLog();
                if (installedLogFilename == null || !File.Exists(installedLogFilename)) return Tuple.Create<string, string>(null, null);

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

        [CanBeNull]
        public static IniFile GetManifest() {
            return TryGetConfig("data_manifest.ini");
        }

        [CanBeNull]
        public static string GetInstalledVersion() {
            return GetManifest()?["VERSION"].GetNonEmpty("SHADERS_PATCH");
        }

        [CanBeNull]
        public static string GetInstalledBuild() {
            return GetManifest()?["VERSION"].GetNonEmpty("SHADERS_PATCH_BUILD");
        }

        [CanBeNull]
        public static string GetActiveBuild() {
            var result = GetInstalledBuild();
            if (result != null && !IsActive()) {
                return null;
            }
            return result;
        }

        public static void Reload() {
            _active = null;
            _configs.Clear();
            _featureSupported.Clear();
            _installed.Reset();
            _audioDescriptionsSet = false;
            ActionExtension.InvokeInMainThreadAsync(() => {
                Reloaded?.Invoke(null, EventArgs.Empty);
                InvalidateFeatures();
                GetExtraAudioLevels();
            });
        }
    }
}