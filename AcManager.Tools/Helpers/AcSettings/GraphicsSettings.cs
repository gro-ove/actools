using System.ComponentModel;
using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class GraphicsSettings : IniPresetableSettings {
        internal GraphicsSettings() : base("graphics", systemConfig: true) {}

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
                value = value.Clamp(-4, 0);
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
            MipLodBias = section.GetInt("MIP_LOD_BIAS", 0);
            SkyboxReflectionGain = section.GetDouble("SKYBOX_REFLECTION_GAIN", 1d).ToIntPercentage();
            MaximumFrameLatency = section.GetInt("MAXIMUM_FRAME_LATENCY", 0);
        }

        [Localizable(false)]
        private bool FixShadowMapBias(IniFileSection section) {
            var result = false;
            if (!section.ContainsKey("SHADOW_MAP_BIAS_0")) {
                result = true;
                section.Set("SHADOW_MAP_BIAS_0", "0.000002");
            }

            if (!section.ContainsKey("SHADOW_MAP_BIAS_1")) {
                result = true;
                section.Set("SHADOW_MAP_BIAS_1", "0.000015");
            }

            if (!section.ContainsKey("SHADOW_MAP_BIAS_2")) {
                result = true;
                section.Set("SHADOW_MAP_BIAS_2", "0.0003");
            }

            return result;
        }

        public void FixShadowMapBias() {
            if (FixShadowMapBias(Ini["DX11"])) {
                SaveImmediately();
            }
        }

        protected override void SetToIni(IniFile ini) {
            var section = ini["DX11"];
            section.Set("ALLOW_UNSUPPORTED_DX10", AllowUnsupportedDx10);
            section.Set("MIP_LOD_BIAS", MipLodBias);
            section.Set("SKYBOX_REFLECTION_GAIN", SkyboxReflectionGain.ToDoublePercentage());
            section.Set("MAXIMUM_FRAME_LATENCY", MaximumFrameLatency);

            FixShadowMapBias(section);
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.VideoPresetChanged();
        }
    }
}
