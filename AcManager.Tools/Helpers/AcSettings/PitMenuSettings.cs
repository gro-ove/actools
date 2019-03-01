namespace AcManager.Tools.Helpers.AcSettings {
    public class PitMenuSettings : IniSettings {
        internal PitMenuSettings() : base("pitstop", systemConfig: true) { }

        private bool _useMousePitstop;

        public bool UseMousePitstop {
            get => _useMousePitstop;
            set => Apply(value, ref _useMousePitstop);
        }

        private bool _stayInCar;

        public bool StayInCar {
            get => _stayInCar;
            set => Apply(value, ref _stayInCar);
        }

        private bool _autoAppOnPitlane;

        public bool AutoAppOnPitlane {
            get => _autoAppOnPitlane;
            set => Apply(value, ref _autoAppOnPitlane);
        }

        private int _visibilityMaxTime;

        public int VisibilityMaxTime {
            get => _visibilityMaxTime;
            set => Apply(value, ref _visibilityMaxTime);
        }

        private int _presetsCount;

        public int PresetsCount {
            get => _presetsCount;
            set => Apply(value, ref _presetsCount);
        }

        protected override void LoadFromIni() {
            UseMousePitstop = Ini["SETTINGS"].GetBool("USE_MOUSE_PITSTOP", false);
            StayInCar = Ini["SETTINGS"].GetBool("STAY_IN_CAR", true);
            AutoAppOnPitlane = Ini["SETTINGS"].GetBool("AUTO_APP_ON_PITLANE", true);
            VisibilityMaxTime = Ini["SETTINGS"].GetInt("VISIBILITY_MAX_TIME", 5);
            PresetsCount = Ini["SETTINGS"].GetInt("PRESETS_COUNT", 5);
        }

        protected override void SetToIni() {
            Ini["SETTINGS"].Set("USE_MOUSE_PITSTOP", UseMousePitstop);
            Ini["SETTINGS"].Set("STAY_IN_CAR", StayInCar);
            Ini["SETTINGS"].Set("AUTO_APP_ON_PITLANE", AutoAppOnPitlane);
            Ini["SETTINGS"].Set("VISIBILITY_MAX_TIME", VisibilityMaxTime);
            Ini["SETTINGS"].Set("PRESETS_COUNT", PresetsCount);
        }
    }
}