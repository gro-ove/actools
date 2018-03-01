using System.IO;
using System.Windows.Forms;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class PluginsSettings : NotifyPropertyChanged {
            internal PluginsSettings() { }

            private bool? _cefFilterAds;

            public bool CefFilterAds {
                get => _cefFilterAds ?? (_cefFilterAds = ValuesStorage.Get("Settings.PluginsSettings.CefFilterAds", false)).Value;
                set {
                    if (Equals(value, _cefFilterAds)) return;
                    _cefFilterAds = value;
                    ValuesStorage.Set("Settings.PluginsSettings.CefFilterAds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _cef60Fps;

            public bool Cef60Fps {
                get => _cef60Fps ?? (_cef60Fps = ValuesStorage.Get("Settings.PluginsSettings.Cef60Fps", true)).Value;
                set {
                    if (Equals(value, _cef60Fps)) return;
                    _cef60Fps = value;
                    ValuesStorage.Set("Settings.PluginsSettings.Cef60Fps", value);
                    OnPropertyChanged();
                }
            }

            private long? _montageMemoryLimit;

            public long MontageMemoryLimit {
                get => _montageMemoryLimit ?? (_montageMemoryLimit = ValuesStorage.Get("Settings.PluginsSettings.MontageMemoryLimit", 2147483648L)).Value;
                set {
                    if (Equals(value, _montageMemoryLimit)) return;
                    _montageMemoryLimit = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageMemoryLimit", value);
                    OnPropertyChanged();
                }
            }

            private long? _montageVramCache;

            public long MontageVramCache {
                get => _montageVramCache ?? (_montageVramCache = ValuesStorage.Get("Settings.PluginsSettings.MontageVramCache", 536870912L)).Value;
                set {
                    if (Equals(value, _montageVramCache)) return;
                    _montageVramCache = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageVramCache", value);
                    OnPropertyChanged();
                }
            }

            public string MontageDefaultTemporaryDirectory => Path.Combine(Path.GetTempPath(), "CMMontage");

            private string _montageTemporaryDirectory;

            public string MontageTemporaryDirectory {
                get => _montageTemporaryDirectory ?? (_montageTemporaryDirectory = ValuesStorage.Get("Settings.PluginsSettings.MontageTemporaryDirectory",
                        MontageDefaultTemporaryDirectory));
                set {
                    value = value.Trim();
                    if (Equals(value, _montageTemporaryDirectory)) return;
                    _montageTemporaryDirectory = value;
                    ValuesStorage.Set("Settings.PluginsSettings.MontageTemporaryDirectory", value);
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _changeMontageTemporaryDirectoryCommand;

            public DelegateCommand ChangeMontageTemporaryDirectoryCommand
                => _changeMontageTemporaryDirectoryCommand ?? (_changeMontageTemporaryDirectoryCommand = new DelegateCommand(() => {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = MontageTemporaryDirectory
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        MontageTemporaryDirectory = dialog.SelectedPath;
                    }
                }));
        }

        private static PluginsSettings _plugins;
        public static PluginsSettings Plugins => _plugins ?? (_plugins = new PluginsSettings());
    }
}