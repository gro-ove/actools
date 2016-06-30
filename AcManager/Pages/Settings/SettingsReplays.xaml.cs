using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsReplays {
        public SettingsReplays() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public AcSettingsHolder.ReplaySettings Replay => AcSettingsHolder.Replay;

            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            private RelayCommand _addReplaysExtensionsCommand;

            public RelayCommand AddReplaysExtensionsCommand => _addReplaysExtensionsCommand ?? (_addReplaysExtensionsCommand = new RelayCommand(o => {
                ReplaysExtensionSetter.RenameAll();
            }, o => ReplaysExtensionSetter.HasWithoutExtension()));
        }
    }
}
