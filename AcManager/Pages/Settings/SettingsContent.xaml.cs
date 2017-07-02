using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsContent {
        public SettingsContent() {
            InitializeComponent();
            DataContext = new ViewModel();
        }


        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.ContentSettings Holder => SettingsHolder.Content;

            internal ViewModel() {}

            public string DefaultTemporaryFilesLocation { get; } = Path.GetTempPath();

            private ICommand _changeTemporaryFilesLocationCommand;

            public ICommand ChangeTemporaryFilesLocationCommand
                => _changeTemporaryFilesLocationCommand ?? (_changeTemporaryFilesLocationCommand = new DelegateCommand(() => {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = SettingsHolder.Content.TemporaryFilesLocation
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        SettingsHolder.Content.TemporaryFilesLocation = dialog.SelectedPath;
                    }
                }));
        }
    }
}
