using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsGenericMods {
        public SettingsGenericMods() {
            InitializeComponent();
            DataContext = new ViewModel();
            /*DirectoryNames.ItemsSource = Directory.GetDirectories(AcRootDirectory.Instance.RequireValue).Select(Path.GetFileName).ApartFrom(
                    "_CommonRedist", "apps", "cfg", "content", "crash_logs", "data",
                    "launcher", "locales", "mods", "rhm", "sdk", "server", "sweetfx", "system");*/
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                GenericMods.SubscribeWeak(OnPropertyChanged);
                UpdateDirectory();
            }

            private string _modsDirectory;

            public string ModsDirectory {
                get => _modsDirectory;
                set {
                    if (Equals(value, _modsDirectory)) return;
                    _modsDirectory = value;
                    OnPropertyChanged();
                }
            }

            private bool _modsDirectoryWrong;

            public bool ModsDirectoryWrong {
                get => _modsDirectoryWrong;
                set {
                    if (Equals(value, _modsDirectoryWrong)) return;
                    _modsDirectoryWrong = value;
                    OnPropertyChanged();
                }
            }

            private void UpdateDirectory() {
                ModsDirectory = GenericMods.GetModsDirectory();
                ModsDirectoryWrong = Directory.Exists(ModsDirectory) &&
                        FileUtils.GetMountPoint(ModsDirectory) != FileUtils.GetMountPoint(AcRootDirectory.Instance.RequireValue);
                if (ModsDirectoryWrong) {
                    Logging.Debug($"{FileUtils.GetMountPoint(ModsDirectory)} ≠ {FileUtils.GetMountPoint(ModsDirectory)}");
                }
            }

            private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(GenericMods.ModsDirectory)) {
                    UpdateDirectory();
                }
            }

            public SettingsHolder.GenericModsSettings GenericMods => SettingsHolder.GenericMods;

            private DelegateCommand _openModsDirectoryCommand;

            public DelegateCommand OpenModsDirectoryCommand => _openModsDirectoryCommand ?? (_openModsDirectoryCommand = new DelegateCommand(() => {
                WindowsHelper.OpenFile(GenericMods.GetModsDirectory());
            }, () => Directory.Exists(GenericMods.GetModsDirectory())));

            private DelegateCommand _changeModsDirectoryCommand;

            public DelegateCommand ChangeModsDirectoryCommand => _changeModsDirectoryCommand ?? (_changeModsDirectoryCommand = new DelegateCommand(() => {
                var dialog = new FolderBrowserDialog {
                    ShowNewFolderButton = true,
                    SelectedPath = Directory.Exists(GenericMods.GetModsDirectory()) ? GenericMods.GetModsDirectory() : AcRootDirectory.Instance.RequireValue
                };

                if (dialog.ShowDialog() == DialogResult.OK) {
                    if (FileUtils.ArePathsEqual(dialog.SelectedPath, AcRootDirectory.Instance.RequireValue)) {
                        ModernDialog.ShowMessage("Can’t use AC root directory");
                        return;
                    }

                    GenericMods.ModsDirectory = dialog.SelectedPath;
                }
            }));
        }
    }
}
