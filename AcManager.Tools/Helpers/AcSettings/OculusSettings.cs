using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class OculusSettings : IniPresetableSettings {
        internal OculusSettings() : base("oculus") {}

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
            AcSettingsHolder.VideoPresetChanged();
        }
    }
}