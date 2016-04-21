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
            public SettingsHolder.DriveSettings Holder => SettingsHolder.Drive;

            public SettingsDriveViewModel() {
                if (!Holder.SelectedStarterType.IsAvailable) {
                    Holder.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
                }
            }

            private RelayCommand _navigateCommand;

            public RelayCommand NavigateCommand => _navigateCommand ?? (_navigateCommand = new RelayCommand(o => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri(o?.ToString() ?? "", UriKind.RelativeOrAbsolute));
            }));

            private RelayCommand _addReplaysExtensionsCommand;

            public RelayCommand AddReplaysExtensionsCommand => _addReplaysExtensionsCommand ?? (_addReplaysExtensionsCommand = new RelayCommand(o => {
                ReplaysExtensionSetter.RenameAll();
            }, o => ReplaysExtensionSetter.HasWithoutExtension()));
        }
    }
}
