using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.About {
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class AboutPage {
        private int _clicks;

        public AboutPage() {
            InitializeComponent();
        }

        private void Version_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Common.DeveloperMode && ++_clicks == 10 &&
                    ModernDialog.ShowMessage("Enable developer mode? Using it might cause data corruption.", "Developer Mode", MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.DeveloperMode = true;
            }
        }
    }
}
