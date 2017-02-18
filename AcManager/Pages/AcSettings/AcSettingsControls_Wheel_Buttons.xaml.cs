using System.Windows;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_Buttons {
        public AcSettingsControls_Wheel_Buttons() {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}
