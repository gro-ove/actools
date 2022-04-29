using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsIntegrated {
        private bool _changed;

        public SettingsIntegrated() {
            DataContext = new ViewModel();
            InitializeComponent();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
            Unloaded += (sender, args) => {
                if (_changed) {
                    Model.Live.UserEntries = Model.Live.UserEntries.ToList();
                    _changed = false;
                }
            };
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.IntegratedSettings Integrated => SettingsHolder.Integrated;
            public SettingsHolder.LiveSettings Live => SettingsHolder.Live;
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            private SettingsHolder.LiveSettings.LiveServiceEntry _selectedLiveService;

            public SettingsHolder.LiveSettings.LiveServiceEntry SelectedLiveService {
                get => _selectedLiveService;
                set => Apply(value, ref _selectedLiveService);
            }

            public ViewModel() {
                _discordOriginal = Integrated.DiscordIntegration;
                Integrated.SubscribeWeak(OnIntegratedPropertyChanged);
            }

            private void OnIntegratedPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(Integrated.DiscordIntegration)) {
                    DiscordRestartRequired = Integrated.DiscordIntegration != _discordOriginal;
                }
            }

            private DelegateCommand _DeleteSelectedServiceCommand;

            public DelegateCommand DeleteSelectedServiceCommand => _DeleteSelectedServiceCommand ?? (_DeleteSelectedServiceCommand = new DelegateCommand(
                    () => Live.UserEntries = Live.UserEntries.ApartFrom(SelectedLiveService).ToList()));

            private AsyncCommand _AddLiveServiceCommand;

            public AsyncCommand AddLiveServiceCommand => _AddLiveServiceCommand ?? (_AddLiveServiceCommand = new AsyncCommand(async () => {
                var url = await Prompt.ShowAsync("Live service URL:", "Add new live service",
                        comment: "Choose an URL which will open by default when you visit the service");
                if (url == null) return;

                if (Live.UserEntries.Any(x => x.Url == url)) {
                    MessageDialog.Show("Service with this URL is already added", "Can’t add new service", MessageDialogButton.OK);
                    return;
                }

                string title;
                using (WaitingDialog.Create("Checking URL…")) {
                    title = await PageTitleFinder.GetPageTitle(url);
                }

                var name = await Prompt.ShowAsync("Live service name:", "Add new live service", title,
                        comment: "Pick a name for its link in Live Services section");
                if (name != null) {
                    Live.UserEntries = Live.UserEntries.Append(new SettingsHolder.LiveSettings.LiveServiceEntry(title)).ToList();
                }
            }));

            private AsyncCommand _importStereoOdometerCommand;

            public AsyncCommand ImportStereoOdometerCommand =>
                    _importStereoOdometerCommand ?? (_importStereoOdometerCommand = new AsyncCommand(async () => {
                        try {
                            using (WaitingDialog.Create("Importing…")) {
                                await Task.Run(() => StereoOdometerHelper.ImportAll());
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t import", e);
                        }
                    }));

            private AsyncCommand _importSidekickOdometerCommand;

            public AsyncCommand ImportSidekickOdometerCommand =>
                    _importSidekickOdometerCommand ?? (_importSidekickOdometerCommand = new AsyncCommand(async () => {
                        try {
                            using (WaitingDialog.Create("Importing…")) {
                                await Task.Run(() => SidekickHelper.OdometerImportAll());
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t import", e);
                        }
                    }));

            private bool _discordOriginal;
            private bool _discordRestartRequired;

            public bool DiscordRestartRequired {
                get => _discordRestartRequired;
                set => Apply(value, ref _discordRestartRequired);
            }

            private DelegateCommand _restartCommand;

            public DelegateCommand RestartCommand => _restartCommand ?? (_restartCommand = new DelegateCommand(WindowsHelper.RestartCurrentApplication));
        }

        private void OnUserLinkTextChanged(object sender, TextChangedEventArgs e) {
            _changed = true;
        }
    }
}