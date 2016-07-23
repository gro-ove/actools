using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class SkidmarksSettings : IniSettings {
        internal SkidmarksSettings() : base("skidmarks", systemConfig: true) {}

        private double _height;

        public double Height {
            get { return _height; }
            set {
                value = value.Clamp(0, 0.1d).Round(0.0001);
                if (Equals(value, _height)) return;
                _height = value;
                OnPropertyChanged();
            }
        }

        private int _quantityMultipler;

        public int QuantityMultipler {
            get { return _quantityMultipler; }
            set {
                value = value.Clamp(0, 1000);
                if (Equals(value, _quantityMultipler)) return;
                _quantityMultipler = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            var section = Ini["GRAPHICS"];
            Height = section.GetDouble("HEIGHT_FROM_GROUND", 0.02);
            QuantityMultipler = section.GetDouble("QUANTITY_MULT", 1d).ToIntPercentage();
        }

        protected override void SetToIni() {
            var section = Ini["GRAPHICS"];
            section.Set("HEIGHT_FROM_GROUND", Height);
            section.Set("QUANTITY_MULT", QuantityMultipler.ToDoublePercentage());
        }
    }
}