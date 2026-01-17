using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Starters;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

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

            public SettingsLive.ViewModel LiveModel { get; } = new SettingsLive.ViewModel();

            public ViewModel() {
                _discordOriginal = Integrated.DiscordIntegration;
                _steamOriginal = Integrated.SteamIntegration;
                Integrated.SubscribeWeak(OnIntegratedPropertyChanged);
            }

            private void OnIntegratedPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(Integrated.DiscordIntegration)) {
                    DiscordRestartRequired = Integrated.DiscordIntegration != _discordOriginal;
                }
                if (e.PropertyName == nameof(Integrated.SteamIntegration)) {
                    SteamRestartRequired = Integrated.SteamIntegration != _steamOriginal;
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
                set => Apply(value, ref _discordRestartRequired);
            }
            
            private bool _steamOriginal;
            private bool _steamRestartRequired;

            public bool SteamRestartRequired {
                get => _steamRestartRequired;
                set => Apply(value, ref _steamRestartRequired);
            }

            private DelegateCommand _restartCommand;

            public DelegateCommand RestartCommand => _restartCommand ?? (_restartCommand = new DelegateCommand(WindowsHelper.RestartCurrentApplication));

            private static bool TryDisableAdminCompatibilitySettings(string acLauncher) {
                FileUtils.TryToDelete(acLauncher + ".config");

                var needsRemoval = false;
                try {
                    using (var stateKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers")) {
                        var value = stateKey?.GetValue(acLauncher)?.ToString();
                        if (value?.Contains("RUNASADMIN") == true) {
                            needsRemoval = true;
                            stateKey.SetValue(acLauncher, "");
                        }
                    }
                } catch (UnauthorizedAccessException) {
                    return !needsRemoval;
                } catch (Exception e) {
                    Logging.Warning(e);
                }
                return true;
            }

            public bool IsSteamFullyIntegrated => SteamStarter.IsFullyIntegrated;

            private DelegateCommand _switchToSteamStarterCommand;

            public DelegateCommand FullIntegrationCommand => _switchToSteamStarterCommand ?? (_switchToSteamStarterCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage(
                    "With full integration, Content Manager will replace original launcher. Don’t worry, if needed, original launcher can still be accessed from Content Manager using a link in the top right corner. To revert it back, just move Content Manager somewhere else and rename “AssettoCorsa_original.exe” back to “AssettoCorsa.exe”, or run Steam integrity check.",
                    "Switch to full integration?", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                try {
                    var acLauncher = AcPaths.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);
                    if (File.Exists(acLauncher)) {
                        var acLauncherNewPlace = Path.Combine(AcRootDirectory.Instance.RequireValue, "AssettoCorsa_original.exe");
                        if (!File.Exists(acLauncherNewPlace)) {
                            File.Move(acLauncher, acLauncherNewPlace);
                        } else {
                            FileUtils.Recycle(acLauncher);
                        }
                    }
                    File.Copy(MainExecutingFile.Location, acLauncher, true);
                    if (!TryDisableAdminCompatibilitySettings(acLauncher)) {
                        NonfatalError.Notify("Failed to move Content Manager executable", "First, disable “Run this program as administrator” compatibility option of original Assetto Corsa launcher.");
                        return;
                    }
                    ProcessExtension.Start(acLauncher, new[] { @"--restart", @"--move-app=" + MainExecutingFile.Location });
                    Environment.Exit(0);
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to move Content Manager executable", "I’m afraid you’ll have to do it manually.", e);
                }
            }
            }, () => !SteamStarter.IsFullyIntegrated));
        }

        private void OnUserLinkTextChanged(object sender, TextChangedEventArgs e) {
            _changed = true;
        }
    }
}