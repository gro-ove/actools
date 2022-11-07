using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class OculusSettings : IniPresetableSettings {
        internal OculusSettings() : base("oculus") {}

        private double _pixelPerDisplay;

        public double PixelPerDisplay {
            get => _pixelPerDisplay;
            set {
                value = value.Clamp(0.1, 10.0);
                if (Equals(value, _pixelPerDisplay)) return;
                _pixelPerDisplay = value;
                OnPropertyChanged();
            }
        }

        private bool _mirrorTexture;

        public bool MirrorTexture {
            get => _mirrorTexture;
            set => Apply(value, ref _mirrorTexture);
        }

        private bool _autoselectRiftAudioDisplay;

        public bool AutoselectRiftAudioDisplay {
            get => _autoselectRiftAudioDisplay;
            set => Apply(value, ref _autoselectRiftAudioDisplay);
        }

        protected override void LoadFromIni() {
            PixelPerDisplay = Ini["SETTINGS"].GetDouble("PIXEL_PER_DISPLAY", 1d);
            AutoselectRiftAudioDisplay = Ini["SETTINGS"].GetBool("AUTOSELECT_RIFT_AUDIO_DEVICE", false);
            MirrorTexture = Ini["MIRROR_TEXTURE"].GetBool("ENABLED", true);
        }

        protected override void SetToIni(IniFile ini) {
            ini["SETTINGS"].Set("PIXEL_PER_DISPLAY", PixelPerDisplay);
            ini["SETTINGS"].Set("AUTOSELECT_RIFT_AUDIO_DEVICE", AutoselectRiftAudioDisplay);
            ini["MIRROR_TEXTURE"].Set("ENABLED", MirrorTexture);
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.VideoPresetChanged();
        }
    }
}