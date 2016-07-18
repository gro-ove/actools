using AcTools.Utils;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class GraphicsSettings : IniSettings {
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

            protected override void LoadFromIni() {
                var section = Ini["DX11"];
                AllowUnsupportedDx10 = section.GetBool("ALLOW_UNSUPPORTED_DX10", false);
                MipLodBias = section.GetDouble("MIP_LOD_BIAS", 0).ToIntPercentage();
                SkyboxReflectionGain = section.GetDouble("SKYBOX_REFLECTION_GAIN", 1d).ToIntPercentage();
            }

            protected override void SetToIni() {
                var section = Ini["DX11"];
                section.Set("ALLOW_UNSUPPORTED_DX10", AllowUnsupportedDx10);
                section.Set("MIP_LOD_BIAS", MipLodBias.ToDoublePercentage());
                section.Set("SKYBOX_REFLECTION_GAIN", SkyboxReflectionGain.ToDoublePercentage());
            }
        }

        private static GraphicsSettings _graphics;

        public static GraphicsSettings Graphics => _graphics ?? (_graphics = new GraphicsSettings());
    }
}
