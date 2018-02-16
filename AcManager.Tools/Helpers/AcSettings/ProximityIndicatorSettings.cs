namespace AcManager.Tools.Helpers.AcSettings {
    public class ProximityIndicatorSettings : IniSettings {
        internal ProximityIndicatorSettings() : base("proximity_indicator", systemConfig: true) {}

        private bool _isEnabled;

        public bool IsEnabled {
            get { return _isEnabled; }
            set => Apply(value, ref _isEnabled);
        }

        protected override void LoadFromIni() {
            IsEnabled = !Ini["SETTINGS"].GetBool("HIDE", false);
        }

        protected override void SetToIni() {
            Ini["SETTINGS"].Set("HIDE", !IsEnabled);
        }
    }
}