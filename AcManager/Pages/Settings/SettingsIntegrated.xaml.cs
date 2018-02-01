using System;
using System.ComponentModel;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsIntegrated {
        public SettingsIntegrated() {
            DataContext = new ViewModel();
            InitializeComponent();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.IntegratedSettings Integrated => SettingsHolder.Integrated;
            public SettingsHolder.LiveSettings Live => SettingsHolder.Live;
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            public ViewModel() {
                _discordOriginal = Integrated.DiscordIntegration;
                Integrated.SubscribeWeak(OnIntegratedPropertyChanged);
            }

            private void OnIntegratedPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(Integrated.DiscordIntegration)) {
                    DiscordRestartRequired = Integrated.DiscordIntegration != _discordOriginal;
                }
            }

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
                set {
                    if (Equals(value, _discordRestartRequired)) return;
                    _discordRestartRequired = value;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _restartCommand;

            public DelegateCommand RestartCommand => _restartCommand ?? (_restartCommand = new DelegateCommand(WindowsHelper.RestartCurrentApplication));
        }
    }
}
