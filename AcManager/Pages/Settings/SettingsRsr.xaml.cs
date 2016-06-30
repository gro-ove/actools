using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsRsr {
        public SettingsRsr() {
            InitializeComponent();
            DataContext = new RsrViewModel();
        }

        public class RsrViewModel : NotifyPropertyChanged {
            public SettingsHolder.LiveTimingSettings LiveTimingSettings => SettingsHolder.LiveTiming;
        }
    }
}
