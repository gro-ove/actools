using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_Buttons {
        public AcSettingsControls_Wheel_Buttons() {
            InitializeComponent();
        }

        private void AcSettingsControls_Wheel_Buttons_OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}
