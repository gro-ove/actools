namespace AcManager.Tools.Helpers.AcSettings {
    public class DamageDisplayerSettings : IniSettings {
        internal DamageDisplayerSettings() : base(@"damage_displayer", systemConfig: true) { }

        private int _x;

        public int X {
            get => _x;
            set {
                if (Equals(value, _x)) return;
                _x = value;
                OnPropertyChanged();
            }
        }

        private int _y;

        public int Y {
            get => _y;
            set {
                if (Equals(value, _y)) return;
                _y = value;
                OnPropertyChanged();
            }
        }

        private int _maxDamageSpeed;

        // Kilometres per hour
        public int MaxDamageSpeed {
            get => _maxDamageSpeed;
            set {
                if (Equals(value, _maxDamageSpeed)) return;
                _maxDamageSpeed = value;
                OnPropertyChanged();
            }
        }

        private int _time;

        public int Time {
            get => _time;
            set {
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
            }
        }

        private bool _printValues;

        public bool PrintValues {
            get => _printValues;
            set {
                if (Equals(value, _printValues)) return;
                _printValues = value;
                OnPropertyChanged();
            }
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