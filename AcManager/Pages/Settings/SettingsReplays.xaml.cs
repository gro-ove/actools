using System.Windows.Input;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsReplays {
        public SettingsReplays() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ReplaySettings Replay => AcSettingsHolder.Replay;

            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            private CommandBase _addReplaysExtensionsCommand;

            public ICommand AddReplaysExtensionsCommand => _addReplaysExtensionsCommand ??
                    (_addReplaysExtensionsCommand = new DelegateCommand(ReplaysExtensionSetter.RenameAll, ReplaysExtensionSetter.HasWithoutExtension));
        }
    }
}
