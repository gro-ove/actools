using System.Windows;
using AcManager.Tools.Objects;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetConditions {
        public ServerPresetConditions() {
            InitializeComponent();
        }

        private void OnAddNewRoundButtonClick(object sender, RoutedEventArgs e) {
            ((SelectedPage.ViewModel)DataContext).SelectedObject.Weather.Add(new ServerWeatherEntry());
        }
    }
}
