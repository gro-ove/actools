using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ServerPresetDriverEntry : NotifyPropertyChanged, IDraggable, IDraggableCloneable {
        private static readonly string DefaultCarId = "abarth500";

        private ServerPresetObject _parent;

        public ServerPresetObject Parent {
            get => _parent;
            set => Apply(value, ref _parent, () => {
                if (value != null) {
                    var setupFileName = _fixedSetup;
                    if (setupFileName != null) {
                        CarSetup = value.SetupItems.FirstOrDefault(x => x.CarId == CarId &&
                                string.Equals(Path.GetFileName(x.Filename), setupFileName, StringComparison.OrdinalIgnoreCase));
                    }
                }
                _fittingSetupsSet = false;
                OnPropertyChanged(nameof(FittingSetups));
            });
        }

        public void RefreshFilteredSetupList() {
            _fittingSetups?.Refresh();
        }

        private string _fixedSetup;

        public ServerPresetDriverEntry(IniFileSection section) {
            CarId = section.GetNonEmpty("MODEL") ?? DefaultCarId;

            var carSkinId = section.GetNonEmpty("SKIN")?.Split('/');
            CarSkinId = carSkinId?[0];
            CspOptions.LoadPacked(carSkinId?.ElementAtOrDefault(1));

            SpectatorMode = section.GetBool("SPECTATOR_MODE", false);
            DriverName = section.GetNonEmpty("DRIVERNAME");
            TeamName = section.GetNonEmpty("TEAM");
            Guid = section.GetNonEmpty("GUID");
            Ballast = section.GetDouble("BALLAST", 0d);
            Restrictor = section.GetDouble("RESTRICTOR", 0d);
            _fixedSetup = section.GetNonEmpty("FIXED_SETUP");

            CspOptions.PropertyChanged += (sender, args) => {
                Logging.Write($"ARG: {args.PropertyName}");

                // ReSharper disable once NotResolvedInText
                OnPropertyChanged(@"CspOptions.Inner");
            };
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

        bool IDraggableCloneable.CanBeCloned => true;

        object IDraggableCloneable.Clone() {
            return Clone();
        }

        public ServerPresetDriverEntry Clone() {
            var s = new IniFileSection(null);
            SaveTo(s, true);
            return new ServerPresetDriverEntry(s);
        }

        public void SaveTo(IniFileSection section, bool allowCspOptions) {
            section.SetOrRemove("MODEL", CarId);
            if (allowCspOptions && CspOptions.Pack(out var packed)) {
                section.Set("SKIN", CarSkinId + "/" + packed);
            } else {
                section.SetOrRemove("SKIN", CarSkinId);
            }
            section.Set("SPECTATOR_MODE", SpectatorMode);
            section.Set("DRIVERNAME", DriverName ?? "");
            section.Set("TEAM", TeamName ?? "");
            section.Set("GUID", Guid ?? "");
            section.Set("BALLAST", Ballast);
            section.Set("RESTRICTOR", Restrictor);

            var setup = _fixedSetup;
            if (string.IsNullOrWhiteSpace(setup)) {
                setup = Path.GetFileName(Parent?.SetupItems.FirstOrDefault(x => x.IsDefault && x.CarId == CarId)?.Filename);
            }
            section.SetOrRemove("FIXED_SETUP", setup);
        }

        private string _carId;

        [NotNull]
        public string CarId {
            get => _carId;
            set {
                // Logging.Debug($"CARID: {value}, {_carId}");
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
            set {
                if (value == null) return;
                CarSkinId = value.Id;
            }
        }

        private bool _spectatorMode;

        public bool SpectatorMode {
            get => _spectatorMode;
            set => Apply(value, ref _spectatorMode);
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
            set => Apply(value, ref _teamName);
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
            set => Apply(value, ref _ballast);
        }

        private double _restrictor;

        public double Restrictor {
            get => _restrictor;
            set => Apply(value, ref _restrictor);
        }

        private ServerPresetObject.SetupItem _carSetup;

        [CanBeNull]
        public ServerPresetObject.SetupItem CarSetup {
            get => _carSetup;
            set => Apply(value, ref _carSetup, () => _fixedSetup = Path.GetFileName(value?.Filename));
        }

        private BetterListCollectionView _fittingSetups;
        private bool _fittingSetupsSet;

        public BetterListCollectionView FittingSetups {
            get {
                if (!_fittingSetupsSet) {
                    _fittingSetupsSet = true;
                    _fittingSetups = Parent != null ? new BetterListCollectionView(Parent.SetupItems) {
                        Filter = x => (x as ServerPresetObject.SetupItem)?.CarId == CarId
                    } : null;
                }
                return _fittingSetups;
            }
        }

        private int _index;

        public int Index {
            get => _index;
            set => Apply(value, ref _index);
        }

        private DelegateCommand _randomSkinCommand;

        public DelegateCommand RandomSkinCommand => _randomSkinCommand ?? (_randomSkinCommand =
                new DelegateCommand(() => CarSkinId = CarObject?.SkinsManager.Enabled.RandomElementOrDefault()?.Id ?? CarSkinId));

        private bool _deleted;

        public bool Deleted {
            get => _deleted;
            set => Apply(value, ref _deleted);
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => { Deleted = true; }));

        private bool _cloned;

        public bool Cloned {
            get => _cloned;
            set => Apply(value, ref _cloned);
        }

        private DelegateCommand _cloneCommand;

        public DelegateCommand CloneCommand => _cloneCommand ?? (_cloneCommand = new DelegateCommand(() => { Cloned = true; }));

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

        public ServerDriverCspOptions CspOptions { get; } = new ServerDriverCspOptions();
    }
}