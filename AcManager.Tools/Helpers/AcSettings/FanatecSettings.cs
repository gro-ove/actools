using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettings {
    public class FanatecSettings : IniSettings {
        internal FanatecSettings() : base(@"fanatec", systemConfig: true) { }

        private bool _enabled;

        public bool Enabled {
            get => _enabled;
            set => Apply(value, ref _enabled);
        }

        private double _gearMaxTime;

        public double GearMaxTime {
            get => _gearMaxTime;
            set => Apply(value.Clamp(0, 10).Round(0.1), ref _gearMaxTime, () => {
                OnPropertyChanged(nameof(DisplayGearMaxTime));
            });
        }

        public string DisplayGearMaxTime {
            get => _gearMaxTime == 10d ? "Always" : _gearMaxTime.ToString("0.##");
            set => GearMaxTime = FlexibleParser.TryParseDouble(value) ?? 10d;
        }

        private bool _showNextGear;

        public bool ShowNextGear {
            get => _showNextGear;
            set => Apply(value, ref _showNextGear);
        }

        private bool _allowToOverridePerCar;

        public bool AllowToOverridePerCar {
            get => _allowToOverridePerCar;
            set => Apply(value, ref _allowToOverridePerCar);
        }

        private bool _guessPerCar;

        public bool GuessPerCar {
            get => _guessPerCar;
            set => Apply(value, ref _guessPerCar);
        }

        protected override void LoadFromIni() {
            Enabled = Ini["SETTINGS"].GetBool("ENABLED", true);
            GearMaxTime = Ini["SETTINGS"].GetDouble("GEAR_MAX_TIME", 1d);
            ShowNextGear = Ini["SETTINGS"].GetBool("SHOW_NEXT_GEAR", false);
            AllowToOverridePerCar = Ini["__CM_SETTINGS"].GetBool("ALLOW_TO_OVERRIDE_PER_CAR", false);
            GuessPerCar = Ini["__CM_SETTINGS"].GetBool("GUESS_PER_CAR", false);
        }

        protected override void SetToIni() {
            Ini["SETTINGS"].Set("ENABLED", Enabled);
            Ini["SETTINGS"].Set("GEAR_MAX_TIME", GearMaxTime >= 10d ? 99999d : GearMaxTime);
            Ini["SETTINGS"].Set("SHOW_NEXT_GEAR", ShowNextGear);
            Ini["SETTINGS"].Remove("__CM_ORIGINAL_ENABLED");
            Ini["SETTINGS"].Remove("__CM_ORIGINAL_GEAR_MAX_TIME");
            Ini["__CM_SETTINGS"].Set("ALLOW_TO_OVERRIDE_PER_CAR", AllowToOverridePerCar);
            Ini["__CM_SETTINGS"].Set("GUESS_PER_CAR", GuessPerCar);
        }
    }
}