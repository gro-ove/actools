using System.IO;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private string _keyUseCustomData;
        private string _keyUseExtendedPhysics;
        private static readonly string KeyUseCustomDataGlobal = "CarObject:UseCustomDataGlobal";
        private static readonly string KeyUseExtendedPhysicsGlobal = "CarObject:UseExtendedPhysicsGlobal";

        private void InitializeCustomDataKeys() {
            if (_keyUseCustomData != null) return;
            _keyUseCustomData = $@"CarObject:{Id}:UseCustomData";
            _keyUseExtendedPhysics = $@"CarObject:{Id}:UseExtendedPhysics";
        }

        public bool SetCustomData(bool allowCustom) {
            var data = Path.Combine(Location, "data.acd");
            var unpacked = Path.Combine(Location, "data");
            var backup = Path.Combine(Location, "data.acd~cm_bak");

            var custom = allowCustom && UseCustomData && SettingsHolder.Drive.QuickDriveAllowCustomData;
            if (SettingsHolder.Drive.QuickDriveAllowCustomData) {
                Logging.Write("Custom data: " + custom);
            }

            if (custom) {
                if (Directory.Exists(unpacked) && File.Exists(data) && !File.Exists(backup)) {
                    File.Move(data, backup);
                    return true;
                }
            } else {
                if (File.Exists(backup) && !File.Exists(data)) {
                    File.Move(backup, data);
                    return true;
                }
            }

            return false;
        }

        public bool SetExtendedPhysics(bool allowCustom) {
            var carIni = AcdData?.GetIniFile("car.ini");
            var existingValue = carIni?["HEADER"].GetNonEmpty("VERSION")?.Contains("extended");
            if (existingValue != !allowCustom) {
                return false;
            }

            var dataAcd = Path.Combine(Location, "data.acd");
            if (File.Exists(dataAcd)) {
                var backupAcd = Path.Combine(Location, "data.acd~cm_bak_ep");
                var custom = allowCustom && UseExtendedPhysics
                        && PatchHelper.IsFeatureSupported(PatchHelper.FeatureFullDay)
                        && SettingsHolder.Drive.QuickDriveAllowExtendedPhysics;
                if (custom) {
                    FileUtils.TryToDelete(backupAcd);
                    if (File.Exists(dataAcd) && !File.Exists(backupAcd)) {
                        File.Copy(dataAcd, backupAcd);
                        carIni["HEADER"].Set("VERSION", "extended-" + carIni["HEADER"].GetNonEmpty("VERSION").As(1));
                        carIni["_EXTENSION"].Set("RAIN_DEV", 1);
                        carIni.Save();
                        return true;
                    }
                } else {
                    if (File.Exists(backupAcd)) {
                        FileUtils.Recycle(dataAcd);
                        File.Move(backupAcd, dataAcd);
                        return true;
                    }
                }
            } else {
                var dataCar = Path.Combine(Location, "data", "car.ini");
                var backupCar = Path.Combine(Location, "data", "car.ini~cm_bak_ep");
                var custom = allowCustom && UseExtendedPhysics;
                if (custom) {
                    FileUtils.TryToDelete(backupCar);
                    if (File.Exists(dataAcd) && !File.Exists(backupCar)) {
                        File.Copy(dataAcd, backupCar);
                        var ini = new IniFile(dataCar);
                        ini["HEADER"].Set("VERSION", "extended-" + carIni["HEADER"].GetNonEmpty("VERSION").As(1));
                        ini["_EXTENSION"].Set("RAIN_DEV", 1);
                        ini.Save();
                        return true;
                    }
                } else {
                    if (File.Exists(backupCar)) {
                        FileUtils.Recycle(dataCar);
                        File.Move(backupCar, dataCar);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool? _useCustomData;

        public bool UseCustomData {
            get {
                InitializeCustomDataKeys();
                return _useCustomData ?? (_useCustomData = ValuesStorage.Get<bool?>(_keyUseCustomData)) ?? UseCustomDataGlobal;
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
            _useCustomData = null;
            ValuesStorage.Remove(_keyUseCustomData);
            OnPropertyChanged(nameof(UseCustomData));
            _resetUseSpecificDataFlagCommand.RaiseCanExecuteChanged();
        }, () => {
            InitializeCustomDataKeys();
            return ValuesStorage.Contains(_keyUseCustomData);
        }));

        private static bool? _useCustomDataGlobal;

        public bool UseCustomDataGlobal {
            get => _useCustomDataGlobal ?? (_useCustomDataGlobal = ValuesStorage.Get<bool>(KeyUseCustomDataGlobal)).Value;
            set {
                if (Equals(value, UseCustomDataGlobal)) return;
                _useCustomDataGlobal = value;
                ValuesStorage.Set(KeyUseCustomDataGlobal, value);
                OnPropertyChanged();
                if (_useCustomData == null) {
                    OnPropertyChanged(nameof(UseCustomData));
                }
            }
        }

        private bool? _useExtendedPhysics;

        public bool UseExtendedPhysics {
            get {
                InitializeCustomDataKeys();
                return _useExtendedPhysics ?? (_useExtendedPhysics = ValuesStorage.Get<bool?>(_keyUseExtendedPhysics)) ?? UseExtendedPhysicsGlobal;
            }
            set {
                if (Equals(value, UseExtendedPhysics)) return;
                _useExtendedPhysics = value;
                OnPropertyChanged();
                ValuesStorage.Set(_keyUseExtendedPhysics, value);
                _resetUseSpecificDataFlagCommand.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _resetUseExtendedPhysicsFlagCommand;

        public DelegateCommand ResetUseExtendedPhysicsFlagCommand => _resetUseExtendedPhysicsFlagCommand ?? (_resetUseExtendedPhysicsFlagCommand = new DelegateCommand(() => {
            InitializeCustomDataKeys();
            _useExtendedPhysics = null;
            ValuesStorage.Remove(_keyUseExtendedPhysics);
            OnPropertyChanged(nameof(UseExtendedPhysics));
            _resetUseExtendedPhysicsFlagCommand.RaiseCanExecuteChanged();
        }, () => {
            InitializeCustomDataKeys();
            return ValuesStorage.Contains(_keyUseExtendedPhysics);
        }));

        private static bool? _useExtendedPhysicsGlobal;

        public bool UseExtendedPhysicsGlobal {
            get => _useExtendedPhysicsGlobal ?? (_useExtendedPhysicsGlobal = ValuesStorage.Get<bool>(KeyUseExtendedPhysicsGlobal)).Value;
            set {
                if (Equals(value, UseExtendedPhysicsGlobal)) return;
                _useExtendedPhysicsGlobal = value;
                ValuesStorage.Set(KeyUseExtendedPhysicsGlobal, value);
                OnPropertyChanged();
                if (_useExtendedPhysics == null) {
                    OnPropertyChanged(nameof(UseExtendedPhysics));
                }
            }
        }
    }
}
