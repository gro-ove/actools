using System;
using System.IO;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Starters;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

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

            public object AlwaysNull {
                get => "";
                set => OnPropertyChanged();
            }

            public ViewModel() {
                if (!Drive.SelectedStarterType.IsAvailable) {
                    Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
                }
            }

            private DelegateCommand _switchToSteamStarterCommand;

            public DelegateCommand SwitchToSteamStarterCommand => _switchToSteamStarterCommand ?? (_switchToSteamStarterCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage(
                    "Steam starter is the fastest and the most reliable way to launch races. Also, with it, Content Manager will be able to check challenges progress more directly, plus it would get Steam overlay. And play time for Assetto Corsa in Steam would be counted the same as with original launcher.[br][br]For it to work, Content Manager will place itself instead of original launcher. Don’t worry, if needed, original launcher will still be accessible from Content Manager, from title links. And you’ll be able to always revert it back, just move Content Manager somewhere else and rename “AssettoCorsa_original.exe” back to “AssettoCorsa.exe”.[br][br]So, would you like to try it?",
                    "Switch to Steam starter?", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
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
                    ProcessExtension.Start(acLauncher, new[] { @"--restart", @"--move-app=" + MainExecutingFile.Location });
                    Environment.Exit(0);
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to move Content Manager executable", "I’m afraid you’ll have to do it manually.", e);
                }
            }
            }, () => !SteamStarter.IsInitialized));
        }
    }
}
