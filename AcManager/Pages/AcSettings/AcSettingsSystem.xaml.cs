using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsSystem {
        public AcSettingsSystem() {
            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public ProximityIndicatorSettings ProximityIndicator => AcSettingsHolder.ProximityIndicator;
            public SessionInfoSettings SessionInfo => AcSettingsHolder.SessionInfo;
            public SkidmarksSettings Skidmarks => AcSettingsHolder.Skidmarks;
            public SystemSettings System => AcSettingsHolder.System;
            public CameraOrbitSettings CameraOrbit => AcSettingsHolder.CameraOrbit;
            public PitMenuSettings PitMenu => AcSettingsHolder.PitMenu;
            public SystemOptionsSettings SystemOptions => AcSettingsHolder.SystemOptions;
            public GhostSettings Ghost => AcSettingsHolder.Ghost;

            private string _ghostDisplayColor;

            public string GhostDisplayColor {
                get => _ghostDisplayColor;
                set => Apply(value, ref _ghostDisplayColor);
            }
        }
    }
}