using System.ComponentModel;
using System.Windows;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsGeneral {
        public SettingsGeneral() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() {}

            public string AcRootDirectoryValue => AcRootDirectory.Instance.Value;

            private RelayCommand _changeAcRootCommand;

            public RelayCommand ChangeAcRootCommand => _changeAcRootCommand ?? (_changeAcRootCommand = new RelayCommand(o => {
                if (ModernDialog.ShowMessage(AppStrings.Settings_General_ChangeAcRoot_Message, AppStrings.Settings_General_ChangeAcRoot,
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                AcRootDirectory.Instance.Reset();
                WindowsHelper.RestartCurrentApplication();
            }));

            private RelayCommand _changeAppKeyCommand;

            public RelayCommand ChangeAppKeyCommand => _changeAppKeyCommand ?? (_changeAppKeyCommand = new RelayCommand(o => {
                new AppKeyDialog().ShowDialog();
            }));

            public SettingsHolder.CommonSettings Common => SettingsHolder.Common;

            public AppUpdater AppUpdater => AppUpdater.Instance;

            public DataUpdater DataUpdater => DataUpdater.Instance;

            private RelayCommand _cleanUpStorageCommand;

            [Localizable(false)]
            public RelayCommand CleanUpStorageCommand => _cleanUpStorageCommand ?? (_cleanUpStorageCommand = new RelayCommand(o => {
                ValuesStorage.Storage.CleanUp(x =>
                        x.StartsWith(".") || 
                        x.StartsWith("KunosCareerObject.SelectedEvent__") ||
                        x.StartsWith("__aclistpageviewmodel_selected_") ||
                        x.StartsWith("__carobject_selectedskin_") ||
                        x.StartsWith("__trackslocator_") ||
                        x.StartsWith("__TimezoneDeterminer_") ||
                        x.StartsWith("LinkGroup.Selected_") ||
                        x.StartsWith("__upgradeiconeditor_") ||
                        x.StartsWith("LinkGroupFilterable.Selected_") ||
                        x.StartsWith("LinkGroupFilterable.RecentlyClosed_") ||
                        x.StartsWith("__qf___online_") ||
                        x.StartsWith("__Online.Sorting__online_") ||
                        x.StartsWith("__online_") ||
                        x.StartsWith("MainWindow__") ||
                        x.StartsWith("__tmp_FontObject.UsingsCarsIds_"));
            }));
        }
    }
}
