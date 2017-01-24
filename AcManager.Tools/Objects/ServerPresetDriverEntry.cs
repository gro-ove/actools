using System;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ServerPresetDriverEntry : NotifyPropertyChanged, IDraggable {
        private static readonly string DefaultCarId = "abarth500";

        public ServerPresetDriverEntry(IniFileSection section) {
            CarId = section.GetNonEmpty("MODEL") ?? DefaultCarId;

            CarSkinId = section.GetNonEmpty("SKIN");
            SpectatorMode = section.GetBool("SPECTATOR_MODE", false);
            DriverName = section.GetNonEmpty("DRIVERNAME");
            TeamName = section.GetNonEmpty("TEAM");
            Guid = section.GetNonEmpty("GUID");
            Ballast = section.GetDouble("BALLAST", 0d);
        }

        public void SaveTo(IniFileSection section) {
            section.Set("MODEL", CarId);
            section.Set("SKIN", CarSkinId);
            section.Set("SPECTATOR_MODE", SpectatorMode);
            section.Set("DRIVERNAME", DriverName);
            section.Set("TEAM", TeamName);
            section.Set("GUID", Guid);
            section.Set("BALLAST", Ballast);
        }

        public ServerPresetDriverEntry([NotNull] CarObject car) {
            CarId = car.Id;
            _carSet = true;
            _carObject = car;

            CarSkinId = car.SkinsManager.WrappersList.FirstOrDefault(x => x.Value.Enabled)?.Id;
        }

        public ServerPresetDriverEntry(ServerSavedDriver saved) {
            CarId = saved.GetCarId() ?? DefaultCarId;
            CarSkinId = saved.GetSkinId(CarId) ?? CarObject?.SkinsManager.WrappersList.FirstOrDefault(x => x.Value.Enabled)?.Id;
            DriverName = saved.DriverName;
            TeamName = saved.TeamName;
            Guid = saved.Guid;
        }

        private string _carId;

        [NotNull]
        public string CarId {
            get { return _carId; }
            set {
                if (value == _carId) return;
                _carId = value;
                _carSet = false;
                _carObject = null;
                _carSkinSet = false;
                _carSkinObject = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CarObject));
            }
        }

        private bool _carSet;
        private CarObject _carObject;

        [CanBeNull]
        public CarObject CarObject {
            get {
                if (!_carSet) {
                    _carSet = true;
                    _carObject = CarsManager.Instance.GetById(CarId);
                }
                return _carObject;
            }
            set {
                CarId = value?.Id ?? DefaultCarId;
                CarSkinId = (value ?? CarObject)?.SkinsManager.WrappersList.FirstOrDefault(x => x.Value.Enabled)?.Id;
            }
        }

        private string _carSkinId;

        [CanBeNull]
        public string CarSkinId {
            get { return _carSkinId; }
            set {
                if (value == _carSkinId) return;
                _carSkinId = value;
                _carSkinSet = false;
                _carSkinObject = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CarSkinObject));
            }
        }

        private bool _carSkinSet;
        private CarSkinObject _carSkinObject;

        [CanBeNull]
        public CarSkinObject CarSkinObject {
            get {
                if (!_carSkinSet) {
                    _carSkinSet = true;
                    _carSkinObject = CarSkinId == null ? null : CarObject?.GetSkinById(CarSkinId);
                }
                return _carSkinObject;
            }
            set { CarSkinId = value?.Id; }
        }

        private bool _spectatorMode;

        public bool SpectatorMode {
            get { return _spectatorMode; }
            set {
                if (value == _spectatorMode) return;
                _spectatorMode = value;
                OnPropertyChanged();
            }
        }

        private string _driverName;
        
        [CanBeNull]
        public string DriverName {
            get { return _driverName; }
            set {
                if (value == _driverName) return;
                _driverName = value;
                OnPropertyChanged();
                _storeCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _teamName;

        [CanBeNull]
        public string TeamName {
            get { return _teamName; }
            set {
                if (value == _teamName) return;
                _teamName = value;
                OnPropertyChanged();
            }
        }

        private string _guid;

        [CanBeNull]
        public string Guid {
            get { return _guid; }
            set {
                if (value == _guid) return;
                _guid = value;
                OnPropertyChanged();
                _storeCommand?.RaiseCanExecuteChanged();
            }
        }

        private double _ballast;

        public double Ballast {
            get { return _ballast; }
            set {
                if (Equals(value, _ballast)) return;
                _ballast = value;
                OnPropertyChanged();
            }
        }

        private string _carSetup;

        [CanBeNull]
        public string CarSetup {
            get { return _carSetup; }
            set {
                if (value == _carSetup) return;
                _carSetup = value;
                OnPropertyChanged();
            }
        }

        private bool _deleted;

        public bool Deleted {
            get { return _deleted; }
            set {
                if (Equals(value, _deleted)) return;
                _deleted = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
        }));

        private DelegateCommand _storeCommand;

        public DelegateCommand StoreCommand => _storeCommand ?? (_storeCommand = new DelegateCommand(() => {
            try {
                ServerPresetsManager.Instance.StoreDriverEntry(this);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t store driver", e);
            }
        }, () => !string.IsNullOrWhiteSpace(Guid) && !string.IsNullOrWhiteSpace(DriverName)));

        public const string DraggableFormat = "Data-ServerPresetDriverEntry";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}