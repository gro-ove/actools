namespace AcManager.Tools.Helpers.AcSettings {
    public class PitStopSettings : IniSettings {
        internal PitStopSettings() : base(@"pitstop", systemConfig: true) {}

        private bool _stayInCar;

        public bool StayInCar {
            get => _stayInCar;
            set => Apply(value, ref _stayInCar);
        }

        private bool _useMouse;

        public bool UseMouse {
            get => _useMouse;
            set => Apply(value, ref _useMouse);
        }

        private int _presetsCount;

        public int PresetsCount {
            get => _presetsCount;
            set => Apply(value, ref _presetsCount);
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