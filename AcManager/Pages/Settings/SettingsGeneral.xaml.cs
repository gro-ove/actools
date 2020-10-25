using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Starters;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsGeneral {
        public ViewModel Model => (ViewModel)DataContext;

        public SettingsGeneral() {
            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class ViewModel : NotifyPropertyChanged {
            public string AcRootDirectoryValue => AcRootDirectory.Instance.Value;

            private string _steamId;

            public string SteamId {
                get => _steamId;
                set => Apply(value, ref _steamId);
            }

            private string _steamProfileName;

            public string SteamProfileName {
                get => _steamProfileName;
                set => Apply(value, ref _steamProfileName);
            }

            internal ViewModel() {
                UpdateSteamId();
                AppShortcutExists = AppShortcut.HasShortcut();
            }

            private bool _appShortcutExists;

            public bool AppShortcutExists {
                get => _appShortcutExists;
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
                SteamProfileName = await SteamIdHelper.GetSteamNameAsync(SteamId);
            }

            private DelegateCommand _openAcRootCommand;

            public DelegateCommand OpenAcRootCommand
                => _openAcRootCommand ?? (_openAcRootCommand = new DelegateCommand(() => { WindowsHelper.OpenFile(AcRootDirectory.Instance.RequireValue); }));

            private DelegateCommand _changeAcRootCommand;

            public DelegateCommand ChangeAcRootCommand => _changeAcRootCommand ?? (_changeAcRootCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage(AppStrings.Settings_General_ChangeAcRoot_Message, AppStrings.Settings_General_ChangeAcRoot,
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                AcRootDirectory.Instance.Reset();
                WindowsHelper.RestartCurrentApplication();
            }));

            private DelegateCommand _changeSteamIdCommand;

            public DelegateCommand ChangeSteamIdCommand => _changeSteamIdCommand ?? (_changeSteamIdCommand = new DelegateCommand(() => {
                if (SteamStarter.IsInitialized) return;
                if (ModernDialog.ShowMessage("Do you want to change Steam ID? RSR and SRS progress are linked to it. Also, app will be restarted.",
                        "Change Steam ID", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                if (new AcRootDirectorySelector(false, true).ShowDialog() == true) {
                    WindowsHelper.RestartCurrentApplication();
                }
            }, () => !SteamStarter.IsInitialized));

            private DelegateCommand _changeAppKeyCommand;

            public DelegateCommand ChangeAppKeyCommand
                => _changeAppKeyCommand ?? (_changeAppKeyCommand = new DelegateCommand(() => { new AppKeyDialog().ShowDialog(); }));

            public SettingsHolder.CommonSettings Common => SettingsHolder.Common;

            public AppUpdater AppUpdater => AppUpdater.Instance;

            public DataUpdater DataUpdater => DataUpdater.Instance;

            private DelegateCommand _cleanUpStorageCommand;

            [Localizable(false)]
            public DelegateCommand CleanUpStorageCommand => _cleanUpStorageCommand ?? (_cleanUpStorageCommand = new DelegateCommand(() => {
                ValuesStorage.Storage.CleanUp(x => x.StartsWith(".") ||
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

            private DelegateCommand _showContentManagerDataCommand;

            public DelegateCommand ShowContentManagerDataCommand => _showContentManagerDataCommand ?? (_showContentManagerDataCommand
                            = new DelegateCommand(() => WindowsHelper.ViewDirectory(FilesStorage.Instance.RootDirectory)));

            private DelegateCommand _showContentManagerLogsCommand;

            public DelegateCommand ShowContentManagerLogsCommand => _showContentManagerLogsCommand ?? (_showContentManagerLogsCommand =
                    new DelegateCommand(() => WindowsHelper.ViewDirectory(Path.Combine(FilesStorage.Instance.RootDirectory, "Logs"))));

            private DelegateCommand _resetDoNotAskAgainsCommand;

            [Localizable(false)]
            public DelegateCommand ResetDoNotAskAgainsCommand => _resetDoNotAskAgainsCommand ?? (_resetDoNotAskAgainsCommand =
                    new DelegateCommand(() => ValuesStorage.Storage.CleanUp(x => x.StartsWith("_stored:__doNotAskAgain_"))));

            private DelegateCommand _resetOccasionalHintsCommand;

            public DelegateCommand ResetOccasionalHintsCommand => _resetOccasionalHintsCommand ?? (_resetOccasionalHintsCommand = new DelegateCommand(() => {
                ValuesStorage.Storage.CleanUp(x => x.StartsWith("__fancyHint:shown:"));
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

        private int _clicks;

        private void OnVersionClick(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Common.DeveloperMode && ++_clicks == 7 &&
                    ModernDialog.ShowMessage(AppStrings.About_DeveloperMode, AppStrings.About_DeveloperMode_Title, MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.DeveloperMode = true;
            }
        }
    }
}