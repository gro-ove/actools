using System.IO;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private string _keyUseCustomData;
        private static readonly string KeyUseCustomDataGlobal = "CarObject:UseCustomDataGlobal";

        private void InitializeCustomDataKeys() {
            if (_keyUseCustomData != null) return;
            _keyUseCustomData = $@"CarObject:{Id}:UseCustomData";
        }

        public bool SetCustomData(bool allowCustom) {
            var data = Path.Combine(Location, "data.acd");
            var unpacked = Path.Combine(Location, "data");
            var backup = Path.Combine(Location, "data.acd~cm_bak");

            var custom = allowCustom && UseCustomData;
            if (custom) {
                if (File.Exists(backup) && !File.Exists(data)) {
                    File.Move(backup, data);
                    return true;
                }
            } else {
                if (Directory.Exists(unpacked) && File.Exists(data) && !File.Exists(backup)) {
                    File.Move(data, backup);
                    return true;
                }
            }

            return false;
        }

        private bool? _useCustomData;

        public bool UseCustomData {
            get {
                InitializeCustomDataKeys();
                return _useCustomData ?? (_useCustomData = ValuesStorage.GetBool(_keyUseCustomData, UseCustomDataGlobal)).Value;
            }
            set {
                if (Equals(value, UseCustomData)) return;
                _useCustomData = value;
                OnPropertyChanged();
                ValuesStorage.Set(_keyUseCustomData, value);
                _resetUseSpecificDataFlagCommand.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _resetUseSpecificDataFlagCommand;

        public DelegateCommand ResetUseSpecificDataFlagCommand => _resetUseSpecificDataFlagCommand ?? (_resetUseSpecificDataFlagCommand = new DelegateCommand(() => {
            InitializeCustomDataKeys();
            _useCustomData = UseCustomDataGlobal;
            ValuesStorage.Remove(_keyUseCustomData);
            OnPropertyChanged(nameof(UseCustomData));
            _resetUseSpecificDataFlagCommand.RaiseCanExecuteChanged();
        }, () => {
            InitializeCustomDataKeys();
            return ValuesStorage.Contains(_keyUseCustomData);
        }));

        private static bool? _useCustomDataGlobal;

        public bool UseCustomDataGlobal {
            get => _useCustomDataGlobal ?? (_useCustomDataGlobal = ValuesStorage.GetBool(KeyUseCustomDataGlobal)).Value;
            set {
                if (Equals(value, UseCustomDataGlobal)) return;
                _useCustomDataGlobal = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyUseCustomDataGlobal, value);
            }
        }
    }
}
