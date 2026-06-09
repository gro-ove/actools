using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class MouseHiderSettings : IniSettings {
        internal MouseHiderSettings() : base("mouse_hider", systemConfig: true) { }

        private double _hideTimeout;

        public double HideTimeout {
            get => _hideTimeout;
            set => Apply(value.Round(0.1), ref _hideTimeout);
        }

        protected override void LoadFromIni() {
            HideTimeout = Ini["SETTINGS"].GetInt("HIDE_INTERVAL", 5000) / 2e3;
        }

        protected override void SetToIni() {
            Ini["SETTINGS"].Set("HIDE_INTERVAL", HideTimeout * 2e3);
        }
    }
}