using AcManager.Controls;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsSystem {
        public AcSettingsSystem() {
            InitializeComponent();
            DataContext = new AcSystemViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class AcSystemViewModel : NotifyPropertyChanged {
            internal AcSystemViewModel() {}

            public ProximityIndicatorSettings ProximityIndicator => AcSettingsHolder.ProximityIndicator;

            public SessionInfoSettings SessionInfo => AcSettingsHolder.SessionInfo;

            public SkidmarksSettings Skidmarks => AcSettingsHolder.Skidmarks;

            public SystemSettings System => AcSettingsHolder.System;

            public SystemOptionsSettings SystemOptions => AcSettingsHolder.SystemOptions;

            public GhostSettings Ghost => AcSettingsHolder.Ghost;

            private string _ghostDisplayColor;

            public string GhostDisplayColor {
                get => _ghostDisplayColor;
                set {
                    if (Equals(value, _ghostDisplayColor)) return;
                    _ghostDisplayColor = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
