using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsLive {
        public SettingsLive() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.LiveSettings LiveSettings => SettingsHolder.Live;
        }
    }
}
