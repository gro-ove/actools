using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsSystem {
        public AcSettingsSystem() {
            InitializeComponent();
            DataContext = new AcSystemViewModel();
        }

        public class AcSystemViewModel : NotifyPropertyChanged {
            internal AcSystemViewModel() {
                // GhostDisplayColor = Va
            }

            public AcSettingsHolder.ProximityIndicatorSettings ProximityIndicator => AcSettingsHolder.ProximityIndicator;

            public AcSettingsHolder.SystemSettings System => AcSettingsHolder.System;

            public AcSettingsHolder.GhostSettings Ghost => AcSettingsHolder.Ghost;

            private string _ghostDisplayColor;

            public string GhostDisplayColor {
                get { return _ghostDisplayColor; }
                set {
                    if (Equals(value, _ghostDisplayColor)) return;
                    _ghostDisplayColor = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
