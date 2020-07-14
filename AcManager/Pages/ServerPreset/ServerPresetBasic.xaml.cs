using System.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetBasic {
        public ServerPresetBasic() {
            InitializeComponent();
            CspVersionAutoFill.IsEnabled = PatchHelper.IsFeatureSupported(PatchHelper.FeatureTestOnline);
            if (SettingsHolder.Online.ServerPresetsFitInFewerTabs) {
                WelcomeMessageTextArea.Height = 80;
            }
        }

        private void CspVersionAutoFillClick(object sender, RoutedEventArgs e) {
            if (DataContext is SelectedPage.ViewModel viewModel) {
                viewModel.SelectedObject.RequiredCspVersion = PatchHelper.GetInstalledBuild().As<int?>(null);
            }
        }
    }
}
