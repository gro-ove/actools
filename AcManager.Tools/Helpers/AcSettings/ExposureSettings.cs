using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class ExposureSettings : IniSettings {
        internal ExposureSettings() : base("exposure") {}

        private int _value;

        public int Value {
            get { return _value; }
            set {
                value = value.Clamp(-100, 300);
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            Value = Ini["EXPOSURE"].GetDouble("VALUE", 1d).ToIntPercentage();
        }

        protected override void SetToIni() {
            Ini["EXPOSURE"].Set("VALUE", Value.ToDoublePercentage());
        }
    }
}