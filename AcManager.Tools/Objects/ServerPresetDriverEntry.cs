using System;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ServerPresetDriverEntry : NotifyPropertyChanged, IDraggable, IDraggableCloneable {
        private static readonly string DefaultCarId = "abarth500";

        public ServerPresetDriverEntry(IniFileSection section) {
            CarId = section.GetNonEmpty("MODEL") ?? DefaultCarId;

            CarSkinId = section.GetNonEmpty("SKIN");
            SpectatorMode = section.GetBool("SPECTATOR_MODE", false);
            DriverName = section.GetNonEmpty("DRIVERNAME");
            TeamName = section.GetNonEmpty("TEAM");
            Guid = section.GetNonEmpty("GUID");
            Ballast = section.GetDouble("BALLAST", 0d);
            Restrictor = section.GetDouble("RESTRICTOR", 0d);
        }

        bool IDraggableCloneable.CanBeCloned => true;

        object IDraggableCloneable.Clone() {
            return Clone();
        }

        public ServerPresetDriverEntry Clone() {
            var s = new IniFileSection(null);
            SaveTo(s);
            return new ServerPresetDriverEntry(s);
        }

        public void SaveTo(IniFileSection section) {
            section.SetOrRemove("MODEL", CarId);
            section.SetOrRemove("SKIN", CarSkinId);
            section.SetOrRemove("SPECTATOR_MODE", SpectatorMode);
            section.SetOrRemove("DRIVERNAME", DriverName);
            section.SetOrRemove("TEAM", TeamName);
            section.SetOrRemove("GUID", Guid);
            section.Set("BALLAST", Ballast);
            section.Set("RESTRICTOR", Restrictor);
        }

        public ServerPresetDriverEntry([NotNull] CarObject car) {
            CarId = car.Id;
            _carSet = true;
            _carObject = car;

            CarSkinId = car.SkinsManager.WrappersList.FirstOrDefault(x => x.Value.Enabled)?.Id;
        }

        public ServerPresetDriverEntry([NotNull] ServerSavedDriver saved) {
            CarId = saved.GetCarId() ?? DefaultCarId;
            CarSkinId = saved.GetSkinId(CarId) ?? CarObject?.SkinsManager.WrappersList.FirstOrDefault(x => x.Value.Enabled)?.Id;
            DriverName = saved.DriverName;
            TeamName = saved.TeamName;
            Guid = saved.Guid;
        }

        private string _carId;

        [NotNull]
        public string CarId {
            get => _carId;
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
            get => _carSkinId;
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
            set => CarSkinId = value?.Id;
        }

        private bool _spectatorMode;

        public bool SpectatorMode {
            get => _spectatorMode;
            set {
                if (value == _spectatorMode) return;
                _spectatorMode = value;
                OnPropertyChanged();
            }
        }

        private string _driverName;

        [CanBeNull]
        public string DriverName {
            get => _driverName;
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
            get => _teamName;
            set {
                if (value == _teamName) return;
                _teamName = value;
                OnPropertyChanged();
            }
        }

        private string _guid;

        [CanBeNull]
        public string Guid {
            get => _guid;
            set {
                if (value == _guid) return;
                _guid = value;
                OnPropertyChanged();
                _storeCommand?.RaiseCanExecuteChanged();
            }
        }

        private double _ballast;

        public double Ballast {
            get => _ballast;
            set {
                if (Equals(value, _ballast)) return;
                _ballast = value;
                OnPropertyChanged();
            }
        }

        private double _restrictor;

        public double Restrictor {
            get => _restrictor;
            set {
                if (Equals(value, _restrictor)) return;
                _restrictor = value;
                OnPropertyChanged();
            }
        }

        private string _carSetup;

        [CanBeNull]
        public string CarSetup {
            get => _carSetup;
            set {
                if (value == _carSetup) return;
                _carSetup = value;
                OnPropertyChanged();
            }
        }

        private int _index;

        public int Index {
            get { return _index; }
            set {
                if (value == _index) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _randomSkinCommand;

        public DelegateCommand RandomSkinCommand => _randomSkinCommand ?? (_randomSkinCommand = new DelegateCommand(() => {
            CarSkinId = CarObject?.SkinsManager.EnabledOnly.RandomElementOrDefault()?.Id ?? CarSkinId;
        }));

        private bool _deleted;

        public bool Deleted {
            get => _deleted;
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

        private bool _cloned;

        public bool Cloned {
            get => _cloned;
            set {
                if (Equals(value, _cloned)) return;
                _cloned = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _cloneCommand;

        public DelegateCommand CloneCommand => _cloneCommand ?? (_cloneCommand = new DelegateCommand(() => {
            Cloned = true;
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