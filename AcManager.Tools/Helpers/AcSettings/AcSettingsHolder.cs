using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.AcSettings {
    public static class AcSettingsHolder {
        #region Apps with presets
        public static readonly string AppsPresetsKey = @"In-Game Apps";
        public static readonly PresetsCategory AppsPresetsCategory = new PresetsCategory(AppsPresetsKey);

        private class AppsPresetsInner : IUserPresetable, IPresetsPreviewProvider {
            private class Saveable {
                public string PythonData, FormsData;
                public bool? DevApps;
            }

            public bool CanBeSaved => true;
            public string PresetableKey => AppsPresetsKey;
            PresetsCategory IUserPresetable.PresetableCategory => AppsPresetsCategory;

            public string ExportToPresetData() {
                return JsonConvert.SerializeObject(new Saveable {
                    PythonData = Python.Export(),
                    FormsData = Forms.Export(),
                    DevApps = System.DeveloperApps
                });
            }

            public event EventHandler Changed;

            public void InvokeChanged() {
                if (_python == null || _forms == null) return;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            internal static IniFile GetFormsIniFromPresetInner(string data) {
                var entry = JsonConvert.DeserializeObject<Saveable>(data);
                return IniFile.Parse(entry.FormsData);
            }

            internal static string CombineAppPresetsInner(IEnumerable<Tuple<ISavedPresetEntry, int>> presets, int selectedDesktop) {
                var list = (from input in presets
                            let s = input.Item1 == null ? null : JsonConvert.DeserializeObject<Saveable>(input.Item1.ReadData())
                            select s == null ? null : new {
                                PythonIni = IniFile.Parse(s.PythonData),
                                FormsIni = IniFile.Parse(s.FormsData),
                                DevAppsEnabled = s.DevApps ?? false,
                                UseFormsFrom = input.Item2
                            }).ToList();
                if (list.Count != 4) throw new Exception("Should be four sets");

                // combine apps
                var python = PythonSettings.Combine(list.NonNull().Select(x => x.PythonIni));
                var forms = FormsSettings.Combine(list.Select(x => x == null ? null : Tuple.Create(x.FormsIni, x.UseFormsFrom)), selectedDesktop);
                return JsonConvert.SerializeObject(new Saveable {
                    PythonData = python.Stringify(),
                    FormsData = forms.Stringify(),
                    DevApps = list.Any(x => x.DevAppsEnabled)
                });
            }

            public void ImportFromPresetData(string data) {
                var entry = JsonConvert.DeserializeObject<Saveable>(data);
                Python.Import(entry.PythonData);
                Forms.Import(entry.FormsData);
                if (SettingsHolder.Drive.SaveDevAppsInAppsPresets && entry.DevApps.HasValue) {
                    System.DeveloperApps = entry.DevApps.Value;
                }
            }

            public object GetPreview(string serializedData) {
                return new TextBlock {
                    Text = FormsSettings.Load(GetFormsIniFromPresetInner(serializedData)).GetVisibleForms() ?? "Empty"
                };
            }
        }

        internal static void AppsPresetChanged() {
            _appsPresets?.InvokeChanged();
        }

        private static AppsPresetsInner _appsPresets;
        public static IUserPresetable AppsPresets => _appsPresets ?? (_appsPresets = new AppsPresetsInner());

        public static IniFile GetFormsIniFromPreset(string appsPresetData) {
            return AppsPresetsInner.GetFormsIniFromPresetInner(appsPresetData);
        }

        public static string CombineAppPresets(IEnumerable<Tuple<ISavedPresetEntry, int>> presets, int selectedDesktop) {
            return AppsPresetsInner.CombineAppPresetsInner(presets, selectedDesktop);
        }


        private static FormsSettings _forms;
        public static FormsSettings Forms => _forms ?? (_forms = new FormsSettings());


        private static PythonSettings _python;
        public static PythonSettings Python => _python ?? (_python = new PythonSettings());
        #endregion

        #region Graphics with presets
        public static readonly string VideoPresetsKey = @"Video Settings";
        public static readonly PresetsCategory VideoPresetsCategory = new PresetsCategory(VideoPresetsKey);

        private class VideoPresetsInner : IUserPresetable {
            private class Saveable {
                public string VideoData, GraphicsData, OculusData;
            }

            public bool CanBeSaved => true;
            public string PresetableKey => VideoPresetsKey;
            PresetsCategory IUserPresetable.PresetableCategory => VideoPresetsCategory;

            public string ExportToPresetData() {
                return JsonConvert.SerializeObject(new Saveable {
                    VideoData = Video.Export(),
                    GraphicsData = Graphics.Export(),
                    OculusData = Oculus.Export()
                });
            }

            public event EventHandler Changed;

            public void InvokeChanged() {
                if (_video == null || _graphics == null || _oculus == null) return;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public void ImportFromPresetData(string data) {
                var entry = JsonConvert.DeserializeObject<Saveable>(data);
                Video.Import(entry.VideoData);
                Graphics.Import(entry.GraphicsData);
                Oculus.Import(entry.OculusData);
            }
        }

        internal static void VideoPresetChanged() {
            _videoPresets?.InvokeChanged();
        }

        private static VideoPresetsInner _videoPresets;
        public static IUserPresetable VideoPresets => _videoPresets ?? (_videoPresets = new VideoPresetsInner());


        private static GraphicsSettings _graphics;
        public static GraphicsSettings Graphics => _graphics ?? (_graphics = new GraphicsSettings());


        private static OculusSettings _oculus;
        public static OculusSettings Oculus => _oculus ?? (_oculus = new OculusSettings());


        private static VideoSettings _video;
        public static VideoSettings Video => _video ?? (_video = new VideoSettings());
        #endregion

        #region Audio with presets
        public static readonly string AudioPresetsKey = @"Audio Settings";
        public static readonly PresetsCategory AudioPresetsCategory = new PresetsCategory(AudioPresetsKey);

        private class AudioPresetsInner : IUserPresetable {
            private class Saveable {
                public string AudioData;
            }

            public bool CanBeSaved => true;
            public string PresetableKey => AudioPresetsKey;
            PresetsCategory IUserPresetable.PresetableCategory => AudioPresetsCategory;

            public string ExportToPresetData() {
                return JsonConvert.SerializeObject(new Saveable {
                    AudioData = Audio.Export()
                });
            }

            public event EventHandler Changed;

            public void InvokeChanged() {
                if (_audio == null) return;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public void ImportFromPresetData(string data) {
                var entry = JsonConvert.DeserializeObject<Saveable>(data);
                Audio.Import(entry.AudioData);
            }
        }

        internal static void AudioPresetChanged() {
            _audioPresets?.InvokeChanged();
        }

        private static AudioPresetsInner _audioPresets;
        public static IUserPresetable AudioPresets => _audioPresets ?? (_audioPresets = new AudioPresetsInner());

        private static AudioSettings _audio;
        public static AudioSettings Audio => _audio ?? (_audio = new AudioSettings());
        #endregion

        #region Miscellaneous
        private static CameraChaseSettings _cameraChase;
        public static CameraChaseSettings CameraChase => _cameraChase ?? (_cameraChase = new CameraChaseSettings());


        private static CameraOnboardSettings _cameraOnboard;
        public static CameraOnboardSettings CameraOnboard => _cameraOnboard ?? (_cameraOnboard = new CameraOnboardSettings());


        private static CameraManagerSettings _cameraManager;
        public static CameraManagerSettings CameraManager => _cameraManager ?? (_cameraManager = new CameraManagerSettings());


        private static CameraOrbitSettings _cameraOrbit;
        public static CameraOrbitSettings CameraOrbit => _cameraOrbit ?? (_cameraOrbit = new CameraOrbitSettings());


        private static FanatecSettings _fanatec;
        public static FanatecSettings Fanatec => _fanatec ?? (_fanatec = new FanatecSettings());


        private static ControlsSettings _controls;
        public static ControlsSettings Controls {
            get {
                if (_controls != null) return _controls;
                _controls = new ControlsSettings();
                _controls.FinishInitialization();
                return _controls;
            }
        }

        private static ExposureSettings _exposure;
        public static ExposureSettings Exposure => _exposure ?? (_exposure = new ExposureSettings());


        private static GameplaySettings _gameplay;
        public static GameplaySettings Gameplay => _gameplay ?? (_gameplay = new GameplaySettings());


        private static GhostSettings _ghost;
        public static GhostSettings Ghost => _ghost ?? (_ghost = new GhostSettings());


        private static LauncherSettings _launcher;
        public static LauncherSettings Launcher => _launcher ?? (_launcher = new LauncherSettings());


        private static ProximityIndicatorSettings _proximityIndicator;
        public static ProximityIndicatorSettings ProximityIndicator => _proximityIndicator ?? (_proximityIndicator = new ProximityIndicatorSettings());


        private static SessionInfoSettings _sessionInfo;
        public static SessionInfoSettings SessionInfo => _sessionInfo ?? (_sessionInfo = new SessionInfoSettings());


        private static ReplaySettings _replay;
        public static ReplaySettings Replay => _replay ?? (_replay = new ReplaySettings());


        private static MessagesSettings _messages;
        public static MessagesSettings Messages => _messages ?? (_messages = new MessagesSettings());


        private static DamageDisplayerSettings _damageDisplayer;
        public static DamageDisplayerSettings DamageDisplayer => _damageDisplayer ?? (_damageDisplayer = new DamageDisplayerSettings());


        private static SkidmarksSettings _skidmarks;
        public static SkidmarksSettings Skidmarks => _skidmarks ?? (_skidmarks = new SkidmarksSettings());


        private static PitStopSettings _pitStop;
        public static PitStopSettings PitStop => _pitStop ?? (_pitStop = new PitStopSettings());


        private static SystemSettings _system;
        public static SystemSettings System => _system ?? (_system = new SystemSettings());


        private static PitMenuSettings _pitMenu;
        public static PitMenuSettings PitMenu => _pitMenu ?? (_pitMenu = new PitMenuSettings());


        private static FfPostProcessSettings _ffPostProcess;
        public static FfPostProcessSettings FfPostProcess => _ffPostProcess ?? (_ffPostProcess = new FfPostProcessSettings());


        private static SystemOptionsSettings _systemOptions;
        public static SystemOptionsSettings SystemOptions => _systemOptions ?? (_systemOptions = new SystemOptionsSettings());
        #endregion

        #region Specific converters
        private class InnerZeroToOffConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                double d;
                return value == null || double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d) && Equals(d, 0d)
                        ? (parameter ?? ToolsStrings.AcSettings_Off) : value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return value == parameter || value as string == ToolsStrings.AcSettings_Off ? 0d : value;
            }
        }

        public static IValueConverter ZeroToOffConverter { get; } = new InnerZeroToOffConverter();
        #endregion
    }
}
