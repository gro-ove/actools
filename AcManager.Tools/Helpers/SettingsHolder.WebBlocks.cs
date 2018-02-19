using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class WebBlocksSettings : NotifyPropertyChanged {
            internal WebBlocksSettings() { }

            private bool? _unloadBeforeRace;

            public bool UnloadBeforeRace {
                get => _unloadBeforeRace ?? (_unloadBeforeRace = ValuesStorage.Get("Settings.WebBlocksSettings.UnloadBeforeRace", true)).Value;
                set {
                    if (Equals(value, _unloadBeforeRace)) return;
                    _unloadBeforeRace = value;
                    ValuesStorage.Set("Settings.WebBlocksSettings.UnloadBeforeRace", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alwaysKeepImportantInMemory;

            public bool AlwaysKeepImportantInMemory {
                get => _alwaysKeepImportantInMemory ??
                        (_alwaysKeepImportantInMemory = ValuesStorage.Get("Settings.WebBlocksSettings.AlwaysKeepImportantInMemory", true)).Value;
                set {
                    if (Equals(value, _alwaysKeepImportantInMemory)) return;
                    _alwaysKeepImportantInMemory = value;
                    ValuesStorage.Set("Settings.WebBlocksSettings.AlwaysKeepImportantInMemory", value);
                    OnPropertyChanged();
                }
            }

            private int? _keepInMemory;

            public int KeepInMemory {
                get => _keepInMemory ?? (_keepInMemory = ValuesStorage.Get("Settings.WebBlocksSettings.KeepInMemory", 5)).Value;
                set {
                    if (Equals(value, _keepInMemory)) return;
                    _keepInMemory = value;
                    ValuesStorage.Set("Settings.WebBlocksSettings.KeepInMemory", value);
                    OnPropertyChanged();
                }
            }

            private bool? _saveMainUrl;

            public bool SaveMainUrl {
                get => _saveMainUrl ?? (_saveMainUrl = ValuesStorage.Get("Settings.WebBlocksSettings.SaveMainUrl", true)).Value;
                set {
                    if (Equals(value, _saveMainUrl)) return;
                    _saveMainUrl = value;
                    ValuesStorage.Set("Settings.WebBlocksSettings.SaveMainUrl", value);
                    OnPropertyChanged();
                }
            }

            private bool? _saveExtraTabs;

            public bool SaveExtraTabs {
                get => _saveExtraTabs ?? (_saveExtraTabs = ValuesStorage.Get("Settings.WebBlocksSettings.SaveExtraTabs", true)).Value;
                set {
                    if (Equals(value, _saveExtraTabs)) return;
                    _saveExtraTabs = value;
                    ValuesStorage.Set("Settings.WebBlocksSettings.SaveExtraTabs", value);
                    OnPropertyChanged();
                }
            }
        }

        private static WebBlocksSettings _webBlocks;
        public static WebBlocksSettings WebBlocks => _webBlocks ?? (_webBlocks = new WebBlocksSettings());
    }
}