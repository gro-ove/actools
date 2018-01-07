using System.Windows;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_System {
        public AcSettingsControls_System() {
            InitializeComponent();
            this.AddWidthCondition(800).Add(x => {
                MainGrid.Columns = x ? 2 : 1;
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}