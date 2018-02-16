namespace AcManager.Tools.Helpers.AcSettings {
    public class DamageDisplayerSettings : IniSettings {
        internal DamageDisplayerSettings() : base(@"damage_displayer", systemConfig: true) { }

        private int _x;

        public int X {
            get => _x;
            set => Apply(value, ref _x);
        }

        private int _y;

        public int Y {
            get => _y;
            set => Apply(value, ref _y);
        }

        private int _maxDamageSpeed;

        // Kilometres per hour
        public int MaxDamageSpeed {
            get => _maxDamageSpeed;
            set => Apply(value, ref _maxDamageSpeed);
        }

        private int _time;

        public int Time {
            get => _time;
            set => Apply(value, ref _time);
        }

        private bool _printValues;

        public bool PrintValues {
            get => _printValues;
            set => Apply(value, ref _printValues);
        }

        protected override void LoadFromIni() {
            var section = Ini["MAIN"];
            Time = section.GetInt("TIME", 10);
            MaxDamageSpeed = section.GetInt("MAX_DAMAGE_KMH", 60);
            X = section.GetInt("POSITION_X", 20);
            Y = section.GetInt("DISTANCE_FROM_CENTER_Y", -10);
            PrintValues = section.GetBool("PRINT_VALUES", false);
        }

        protected override void SetToIni() {
            var section = Ini["MAIN"];
            section.Set("TIME", Time);
            section.Set("MAX_DAMAGE_KMH", MaxDamageSpeed);
            section.Set("POSITION_X", X);
            section.Set("DISTANCE_FROM_CENTER_Y", Y);
            section.Set("PRINT_VALUES", PrintValues);
        }
    }
}