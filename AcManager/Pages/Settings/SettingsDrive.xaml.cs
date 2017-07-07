using System;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
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
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
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
                    (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri(o, UriKind.RelativeOrAbsolute));
                }
            }));
        }
    }
}
