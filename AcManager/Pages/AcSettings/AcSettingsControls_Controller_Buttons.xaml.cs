using System.Windows;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Controller_Buttons {
        public AcSettingsControls_Controller_Buttons() {
            InitializeComponent();

            this.AddWidthCondition(900).Add(x => {
                MainGrid.Columns = x ? 2 : 1;
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}
