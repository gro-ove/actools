using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettingsControls;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_Main {
        public AcSettingsControls_Wheel_Main() {
            InitializeComponent();
        }

        private bool _loaded;

        private void AcSettingsControls_Wheel_Main_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            AcSettingsHolder.Controls.Used++;
        }

        private void AcSettingsControls_Wheel_Main_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            AcSettingsHolder.Controls.Used--;
            AcSettingsHolder.Controls.StopWaiting();
        }

        private void AcSettingsControls_Wheel_Main_OnPreviewKeyDown(object sender, KeyEventArgs e) {
        }
    }
}
