using System;
using System.Windows;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using Microsoft.Win32;

namespace AcManager.Pages.Settings {
    public partial class SettingsDrive {
        public ViewModel Model => (ViewModel)DataContext;

        public SettingsDrive() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ReplaySettings Replay => AcSettingsHolder.Replay;

            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            public ViewModel() {
                if (!Drive.SelectedStarterType.IsAvailable) {
                    Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
                }
            }

            private CommandBase _navigateCommand;

            public ICommand NavigateCommand => _navigateCommand ?? (_navigateCommand = new DelegateCommand<string>(o => {
                if (o == null) return;
                if (o.StartsWith(@"http")) {
                    WindowsHelper.ViewInBrowser(o);
                } else {
                    (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri(o, UriKind.RelativeOrAbsolute));
                }
            }));

            private DelegateCommand _selectRhmLocationCommand;

            public DelegateCommand SelectRhmLocationCommand => _selectRhmLocationCommand ?? (_selectRhmLocationCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = "Real Head Motion|RealHeadMotionAssettoCorsa.exe|Applications (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Real Head Motion Application"
                };

                if (dialog.ShowDialog() == true) {
                    Drive.RhmLocation = dialog.FileName;
                }
            }));
        }
    }
}
