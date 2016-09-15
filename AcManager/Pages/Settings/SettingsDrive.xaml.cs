using System;
using System.Windows;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

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

            private ProperCommand _navigateCommand;

            public ICommand NavigateCommand => _navigateCommand ?? (_navigateCommand = new ProperCommand(o => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri(o?.ToString() ?? "", UriKind.RelativeOrAbsolute));
            }));
        }
    }
}
