using System;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsGeneral {
        public SettingsGeneral() {
            InitializeComponent();
            DataContext = new GeneralViewModel();
        }


        public class GeneralViewModel
            : NotifyPropertyChanged {
            internal GeneralViewModel() {
                // SimpleSkinsMode = 
            }

            public string AcRootDirectoryValue => AcRootDirectory.Instance.Value;

            private RelayCommand _changeAcRootCommand;

            public RelayCommand ChangeAcRootCommand => _changeAcRootCommand ?? (_changeAcRootCommand = new RelayCommand(o => {
                if (ModernDialog.ShowMessage("Do you want to change path? App will be restarted.", "Change path to AC root folder", MessageBoxButton.YesNo) !=
                        MessageBoxResult.Yes) return;
                AcRootDirectory.Instance.Reset();
                WindowsHelper.RestartCurrentApplication();
            }, o => true));

            public SettingsHolder.CommonSettings CommonSettings => SettingsHolder.Common;

            public AppUpdater AppUpdater => AppUpdater.Instance;

            public ContentSyncronizer ContentSyncronizer => ContentSyncronizer.Instance;

            public ValuesStorage ValuesStorage => ValuesStorage.Instance;

            private RelayCommand _cleanUpStorageCommand;

            public RelayCommand CleanUpStorageCommand => _cleanUpStorageCommand ?? (_cleanUpStorageCommand = new RelayCommand(o => {
                ValuesStorage.CleanUp(x => x.StartsWith("KunosCareerObject.SelectedEvent__") ||
                        x.StartsWith("__aclistpageviewmodel_selected_") ||
                        x.StartsWith("__carobject_selectedskin_") ||
                        x.StartsWith("__trackslocator_") ||
                        x.StartsWith("__TimezoneDeterminer_") ||
                        x.StartsWith("__upgradeiconeditor_"));
            }));

            private const string KeyLogsSentTime = "GeneralViewModel.KeyLogsSentTime";

            private AsyncCommand _sendLogsCommand;

            public AsyncCommand SendLogsCommand => _sendLogsCommand ?? (_sendLogsCommand = new AsyncCommand(async o => {
                try {
                    await Task.Run(() => AppReporter.SendLogs());
                    ValuesStorage.Set(KeyLogsSentTime, DateTime.Now);
                    Toast.Show("Logs Sent", "Thank you for help");
                } catch (Exception e) {
                    NonfatalError.Notify("Can't send logs", e);
                }
            }, o => DateTime.Now - ValuesStorage.GetDateTimeOrEpochTime(KeyLogsSentTime) > TimeSpan.FromHours(0.0001), 3000));
        }
    }
}
