using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_ForceFeedback {
        public AcSettingsControls_Wheel_ForceFeedback() {
            DataContext = new AcControlsViewModel();
            InitializeComponent();
        }

        public class AcControlsViewModel : NotifyPropertyChanged {
            internal AcControlsViewModel() { }

            public ControlsSettings Controls => AcSettingsHolder.Controls;

            public SystemSettings System => AcSettingsHolder.System;
        }
    }
}
