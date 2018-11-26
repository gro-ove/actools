using System.Windows;
using System.Windows.Controls.Primitives;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Controller_Main {
        public AcSettingsControls_Controller_Main() {
            InitializeComponent();

            this.AddWidthCondition(720).Add(x => {
                MainGrid.FindVisualChild<UniformGrid>()?.SetValue(UniformGrid.ColumnsProperty, x ? 2 : 1);
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}
