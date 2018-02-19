using System.IO;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class GenericModsSettings : NotifyPropertyChanged {
            internal GenericModsSettings() { }

            private bool? _useHardLinks;

            public bool UseHardLinks {
                get => _useHardLinks ?? (_useHardLinks = ValuesStorage.Get("Settings.GenericModsSettings.UseHardLinks", true)).Value;
                set {
                    if (Equals(value, _useHardLinks)) return;
                    _useHardLinks = value;
                    ValuesStorage.Set("Settings.GenericModsSettings.UseHardLinks", value);
                    OnPropertyChanged();
                }
            }

            private bool? _detectWhileInstalling;

            public bool DetectWhileInstalling {
                get => _detectWhileInstalling ??
                        (_detectWhileInstalling = ValuesStorage.Get("Settings.GenericModsSettings.DetectWhileInstalling", true)).Value;
                set {
                    if (Equals(value, _detectWhileInstalling)) return;
                    _detectWhileInstalling = value;
                    ValuesStorage.Set("Settings.GenericModsSettings.DetectWhileInstalling", value);
                    OnPropertyChanged();
                }
            }

            private string _modsDirectory;

            public string ModsDirectory {
                get => _modsDirectory ??
                        (_modsDirectory = ValuesStorage.Get("Settings.GenericModsSettings.ModsDirectory", "mods"));
                set {
                    value = value.Trim();
                    if (Equals(value, _modsDirectory)) return;
                    _modsDirectory = value;
                    ValuesStorage.Set("Settings.GenericModsSettings.ModsDirectory", value);
                    OnPropertyChanged();
                }
            }

            public string GetModsDirectory() {
                var value = ModsDirectory;

                if (string.IsNullOrWhiteSpace(value)) {
                    value = "mods";
                }

                if (!Path.IsPathRooted(value)) {
                    value = Path.Combine(AcRootDirectory.Instance.RequireValue, value);
                }

                return value;
            }
        }

        private static GenericModsSettings _genericMods;
        public static GenericModsSettings GenericMods => _genericMods ?? (_genericMods = new GenericModsSettings());
    }
}