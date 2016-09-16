using System;
using System.Windows;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Commands;
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

            private ICommandExt _navigateCommand;

            public ICommand NavigateCommand => _navigateCommand ?? (_navigateCommand = new DelegateCommand<object>(o => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri(o?.ToString() ?? "", UriKind.RelativeOrAbsolute));
            }));
        }
    }
}
