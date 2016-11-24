using System;
using System.Globalization;
using System.Windows.Data;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.AcSettings {
    public static class AcSettingsHolder {
        #region Apps with presets
        private class AppsPresetsInner : IUserPresetable {
            private class Saveable {
                public string PythonData, FormsData;
            }

            public bool CanBeSaved => true;

            public string PresetableKey => @"In-Game Apps";

            string IUserPresetable.PresetableCategory => PresetableKey;

            string IUserPresetable.DefaultPreset => null;

            public string ExportToPresetData() {
                return JsonConvert.SerializeObject(new Saveable {
                    PythonData = Python.Export(),
                    FormsData = Forms.Export()
                });
            }

            public event EventHandler Changed;

            public void InvokeChanged() {
                if (_python == null || _forms == null) return;
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public void ImportFromPresetData(string data) {
                var entry = JsonConvert.DeserializeObject<Saveable>(data);
                Python.Import(entry.PythonData);
                Forms.Import(entry.FormsData);
            }
        }

        internal static void AppsPresetChanged() {
            _appsPresets?.InvokeChanged();
        }

        private static AppsPresetsInner _appsPresets;
        public static IUserPresetable AppsPresets => _appsPresets ?? (_appsPresets = new AppsPresetsInner());


        private static FormsSettings _forms;
        public static FormsSettings Forms => _forms ?? (_forms = new FormsSettings());


        private static PythonSettings _python;
        public static PythonSettings Python => _python ?? (_python = new PythonSettings());
        #endregion

        #region Graphics with presets
        private class VideoPresetsInner : IUserPresetable {
            private class Saveable {
                public string VideoData, GraphicsData, OculusData;
            }

            public bool CanBeSaved => true;

            public string PresetableKey => @"Video Settings";

            string IUserPresetable.PresetableCategory => PresetableKey;

            string IUserPresetable.DefaultPreset => null;

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

        #region Miscellaneous
        private static AudioSettings _audio;
        public static AudioSettings Audio => _audio ?? (_audio = new AudioSettings());


        private static CameraOnboardSettings _cameraOnboard;
        public static CameraOnboardSettings CameraOnboard => _cameraOnboard ?? (_cameraOnboard = new CameraOnboardSettings());


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


        private static ProximityIndicatorSettings _proximityIndicator;
        public static ProximityIndicatorSettings ProximityIndicator => _proximityIndicator ?? (_proximityIndicator = new ProximityIndicatorSettings());


        private static SessionInfoSettings _sessionInfo;
        public static SessionInfoSettings SessionInfo => _sessionInfo ?? (_sessionInfo = new SessionInfoSettings());


        private static ReplaySettings _replay;
        public static ReplaySettings Replay => _replay ?? (_replay = new ReplaySettings());


        private static SkidmarksSettings _skidmarks;
        public static SkidmarksSettings Skidmarks => _skidmarks ?? (_skidmarks = new SkidmarksSettings());


        private static SystemSettings _system;
        public static SystemSettings System => _system ?? (_system = new SystemSettings());
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
