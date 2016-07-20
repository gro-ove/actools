using System;
using AcTools.DataFile;
using AcTools.Utils;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class GraphicsSettings : IniPresetableSettings {
            internal GraphicsSettings() : base(@"graphics", systemConfig: true) { }

            private bool _allowUnsupportedDx10;

            public bool AllowUnsupportedDx10 {
                get { return _allowUnsupportedDx10; }
                set {
                    if (Equals(value, _allowUnsupportedDx10)) return;
                    _allowUnsupportedDx10 = value;
                    OnPropertyChanged();
                }
            }

            private int _mipLodBias;

            public int MipLodBias {
                get { return _mipLodBias; }
                set {
                    value = value.Clamp(-400, 0);
                    if (Equals(value, _mipLodBias)) return;
                    _mipLodBias = value;
                    OnPropertyChanged();
                }
            }

            private int _skyboxReflectionGain;

            public int SkyboxReflectionGain {
                get { return _skyboxReflectionGain; }
                set {
                    value = value.Clamp(-1000, 9000);
                    if (Equals(value, _skyboxReflectionGain)) return;
                    _skyboxReflectionGain = value;
                    OnPropertyChanged();
                }
            }

            private int _maximumFrameLatency;

            public int MaximumFrameLatency {
                get { return _maximumFrameLatency; }
                set {
                    value = value.Clamp(0, 10);
                    if (Equals(value, _maximumFrameLatency)) return;
                    _maximumFrameLatency = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                var section = Ini["DX11"];
                AllowUnsupportedDx10 = section.GetBool("ALLOW_UNSUPPORTED_DX10", false);
                MipLodBias = section.GetDouble("MIP_LOD_BIAS", 0).ToIntPercentage();
                SkyboxReflectionGain = section.GetDouble("SKYBOX_REFLECTION_GAIN", 1d).ToIntPercentage();
                MaximumFrameLatency = section.GetInt("MAXIMUM_FRAME_LATENCY", 0);
            }

            protected override void SetToIni(IniFile ini) {
                var section = ini["DX11"];
                section.Set("ALLOW_UNSUPPORTED_DX10", AllowUnsupportedDx10);
                section.Set("MIP_LOD_BIAS", MipLodBias.ToDoublePercentage());
                section.Set("SKYBOX_REFLECTION_GAIN", SkyboxReflectionGain.ToDoublePercentage());
                section.Set("MAXIMUM_FRAME_LATENCY", MaximumFrameLatency);
            }

            protected override void InvokeChanged() {
                _videoPresets?.InvokeChanged();
            }
        }

        private static GraphicsSettings _graphics;

        public static GraphicsSettings Graphics => _graphics ?? (_graphics = new GraphicsSettings());

        public class OculusSettings : IniPresetableSettings {
            internal OculusSettings() : base(@"oculus") { }

            private double _pixelPerDisplay;

            public double PixelPerDisplay {
                get { return _pixelPerDisplay; }
                set {
                    value = value.Clamp(0.1, 10.0);
                    if (Equals(value, _pixelPerDisplay)) return;
                    _pixelPerDisplay = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                PixelPerDisplay = Ini["SETTINGS"].GetDouble("PIXEL_PER_DISPLAY", 1d);
            }

            protected override void SetToIni(IniFile ini) {
                ini["SETTINGS"].Set("PIXEL_PER_DISPLAY", PixelPerDisplay);
            }

            protected override void InvokeChanged() {
                _videoPresets?.InvokeChanged();
            }
        }

        private static OculusSettings _oculus;

        public static OculusSettings Oculus => _oculus ?? (_oculus = new OculusSettings());

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

        private static VideoPresetsInner _videoPresets;

        public static IUserPresetable VideoPresets => _videoPresets ?? (_videoPresets = new VideoPresetsInner());
    }
}
