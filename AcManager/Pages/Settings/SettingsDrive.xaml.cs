using System;
using System.Windows;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsDrive {
        private SettingsDriveViewModel Model => (SettingsDriveViewModel)DataContext;

        public SettingsDrive() {
            DataContext = new SettingsDriveViewModel();
            InitializeComponent();
        }

        public class SettingsDriveViewModel : NotifyPropertyChanged {
            public AcSettingsHolder.ReplaySettings Replay => AcSettingsHolder.Replay;

            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            public SettingsDriveViewModel() {
                if (!Drive.SelectedStarterType.IsAvailable) {
                    Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
                }
            }

            private RelayCommand _navigateCommand;

            public RelayCommand NavigateCommand => _navigateCommand ?? (_navigateCommand = new RelayCommand(o => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri(o?.ToString() ?? "", UriKind.RelativeOrAbsolute));
            }));
        }
    }
}
