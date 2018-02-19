using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class InterfaceSettings : NotifyPropertyChanged {
            internal InterfaceSettings() { }

            private bool? _quickDriveFastAccessButtons;

            public bool QuickDriveFastAccessButtons {
                get => _quickDriveFastAccessButtons ??
                        (_quickDriveFastAccessButtons = ValuesStorage.Get("Settings.InterfaceSettings.QuickDriveFastAccessButtons", true)).Value;
                set {
                    if (Equals(value, _quickDriveFastAccessButtons)) return;
                    _quickDriveFastAccessButtons = value;
                    ValuesStorage.Set("Settings.InterfaceSettings.QuickDriveFastAccessButtons", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skinsSetupsNewWindow;

            public bool SkinsSetupsNewWindow {
                get => _skinsSetupsNewWindow ?? (_skinsSetupsNewWindow = ValuesStorage.Get("Settings.InterfaceSettings.SkinsSetupsNewWindow", false)).Value;
                set {
                    if (Equals(value, _skinsSetupsNewWindow)) return;
                    _skinsSetupsNewWindow = value;
                    ValuesStorage.Set("Settings.InterfaceSettings.SkinsSetupsNewWindow", value);
                    OnPropertyChanged();
                }
            }
        }

        private static InterfaceSettings _interface;
        public static InterfaceSettings Interface => _interface ?? (_interface = new InterfaceSettings());
    }
}