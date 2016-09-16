using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsGeneral {
        public ViewModel Model => (ViewModel)DataContext;

        public SettingsGeneral() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public string AcRootDirectoryValue => AcRootDirectory.Instance.Value;

            private string _steamId;

            public string SteamId {
                get { return _steamId; }
                set {
                    if (Equals(value, _steamId)) return;
                    _steamId = value;
                    OnPropertyChanged();
                }
            }

            private string _steamProfileName;

            public string SteamProfileName {
                get { return _steamProfileName; }
                set {
                    if (Equals(value, _steamProfileName)) return;
                    _steamProfileName = value;
                    OnPropertyChanged();
                }
            }

            internal ViewModel() {
                UpdateSteamId();
                AppShortcutExists = AppShortcut.HasShortcut();
            }

            private bool _appShortcutExists;

            public bool AppShortcutExists {
                get { return _appShortcutExists; }
                set {
                    if (Equals(value, _appShortcutExists)) return;
                    _appShortcutExists = value;
                    OnPropertyChanged();

                    if (value) {
                        AppShortcut.CreateShortcut();
                    } else {
                        AppShortcut.DeleteShortcut();
                    }
                }
            }

            public void Load() {
                SteamIdHelper.Instance.PropertyChanged += SteamIdHelper_PropertyChanged;
            }

            public void Unload() {
                SteamIdHelper.Instance.PropertyChanged -= SteamIdHelper_PropertyChanged;
            }

            private void SteamIdHelper_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(SteamIdHelper.Value)) {
                    UpdateSteamId();
                }
            }

            private async void UpdateSteamId() {
                SteamId = SteamIdHelper.Instance.Value;
                SteamProfileName = await SteamIdHelper.GetSteamName(SteamId);
            }

            private ICommand _changeAcRootCommand;

            public ICommand ChangeAcRootCommand => _changeAcRootCommand ?? (_changeAcRootCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage(AppStrings.Settings_General_ChangeAcRoot_Message, AppStrings.Settings_General_ChangeAcRoot,
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                AcRootDirectory.Instance.Reset();
                WindowsHelper.RestartCurrentApplication();
            }));

            private ICommand _changeSteamIdCommand;

            public ICommand ChangeSteamIdCommand => _changeSteamIdCommand ?? (_changeSteamIdCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage("Do you want to change Steam ID? App will be restarted; also, RSR and SRS progress will be nulled.",
                                "Change Steam ID", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                new AcRootDirectorySelector(false, true).ShowDialog();
                WindowsHelper.RestartCurrentApplication();
            }));

            private ICommand _changeAppKeyCommand;

            public ICommand ChangeAppKeyCommand => _changeAppKeyCommand ?? (_changeAppKeyCommand = new DelegateCommand(() => {
                new AppKeyDialog().ShowDialog();
            }));

            public SettingsHolder.CommonSettings Common => SettingsHolder.Common;

            public AppUpdater AppUpdater => AppUpdater.Instance;

            public DataUpdater DataUpdater => DataUpdater.Instance;

            private ICommand _cleanUpStorageCommand;

            [Localizable(false)]
            public ICommand CleanUpStorageCommand => _cleanUpStorageCommand ?? (_cleanUpStorageCommand = new DelegateCommand(() => {
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

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            Model.Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            Model.Unload();
        }
    }
}
