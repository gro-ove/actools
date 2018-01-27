using System.Windows;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_Buttons {
        public AcSettingsControls_Wheel_Buttons() {
            InitializeComponent();

            this.AddWidthCondition(600).Add(x => {
                HShifterButtons.FindVisualChild<UniformGridWithOrientation>()?.SetValue(SpacingUniformGrid.RowsProperty, x ? 2 : 4);
            });

            this.AddWidthCondition(900).Add(x => {
                MainGrid.Columns = x ? 2 : 1;
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }
    }
}
