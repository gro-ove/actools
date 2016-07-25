using System.Windows;
using AcManager.Tools.Helpers.AcSettings;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_Main {
        public AcSettingsControls_Wheel_Main() {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}
