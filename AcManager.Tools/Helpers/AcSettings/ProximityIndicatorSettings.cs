namespace AcManager.Tools.Helpers.AcSettings {
    public class ProximityIndicatorSettings : IniSettings {
        internal ProximityIndicatorSettings() : base("proximity_indicator", systemConfig: true) {}

        private bool _isEnabled;

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            IsEnabled = !Ini["SETTINGS"].GetBool("HIDE", false);
        }

        protected override void SetToIni() {
            Ini["SETTINGS"].Set("HIDE", !IsEnabled);
        }
    }
}