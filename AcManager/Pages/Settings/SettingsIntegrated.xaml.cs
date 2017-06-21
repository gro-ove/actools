using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsIntegrated {
        public SettingsIntegrated() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.IntegratedSettings Integrated => SettingsHolder.Integrated;
            public SettingsHolder.LiveSettings Live => SettingsHolder.Live;
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;
        }
    }
}
