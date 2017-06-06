namespace AcManager.Tools.Helpers.AcSettings {
    public class PitStopSettings : IniSettings {
        internal PitStopSettings() : base(@"pitstop", systemConfig: true) {}

        private bool _stayInCar;

        public bool StayInCar {
            get { return _stayInCar; }
            set {
                if (Equals(value, _stayInCar)) return;
                _stayInCar = value;
                OnPropertyChanged();
            }
        }

        private bool _useMouse;

        public bool UseMouse {
            get { return _useMouse; }
            set {
                if (Equals(value, _useMouse)) return;
                _useMouse = value;
                OnPropertyChanged();
            }
        }

        private int _presetsCount;

        public int PresetsCount {
            get { return _presetsCount; }
            set {
                if (Equals(value, _presetsCount)) return;
                _presetsCount = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            var section = Ini["SETTINGS"];
            UseMouse = section.GetBool("USE_MOUSE_PITSTOP", false);
            StayInCar = section.GetBool("STAY_IN_CAR", false);
            PresetsCount = section.GetInt("PRESETS_COUNT", 3);
        }

        protected override void SetToIni() {
            var section = Ini["SETTINGS"];
            section.Set("USE_MOUSE_PITSTOP", UseMouse);
            section.Set("STAY_IN_CAR", StayInCar);
            section.Set("PRESETS_COUNT", PresetsCount);
        }
    }
}