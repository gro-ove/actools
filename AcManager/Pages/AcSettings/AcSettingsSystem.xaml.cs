using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsSystem {
        public AcSettingsSystem() {
            InitializeComponent();
            DataContext = new AcSystemViewModel();
        }

        public class AcSystemViewModel : NotifyPropertyChanged {
            internal AcSystemViewModel() { }

            public AcSettingsHolder.ProximityIndicatorSettings ProximityIndicator => AcSettingsHolder.ProximityIndicator;

            public AcSettingsHolder.SystemSettings System => AcSettingsHolder.System;
        }
    }
}
