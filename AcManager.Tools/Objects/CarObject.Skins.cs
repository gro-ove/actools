using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject : IAcManagerScanWrapper {
        #region Initialization
        public string SkinsDirectory { get; private set; }

        private CarSkinsManager InitializeSkins() {
            var m = Measure();
            var manager = new CarSkinsManager(Id, new InheritingAcDirectories(FileAcManager.Directories, SkinsDirectory), OnSkinsCollectionReady) {
                ScanWrapper = this
            };
            manager.Created += OnSkinsManagerCreated;
            m?.Step("Ready");
            return manager;
        }

        private void OnSkinsCollectionReady(object sender, EventArgs e) {
            var any = SkinsManager.GetDefault();
            ErrorIf(any == null, AcErrorType.CarSkins_SkinsAreMissing);
            if (any == null) {
                SelectedSkin = null;
            } else if (SelectedSkin == null) {
                SelectedSkin = any;
            }
        }

        private readonly CompositeObservableCollection<IAcError> _errors = new CompositeObservableCollection<IAcError>();
        public override ObservableCollection<IAcError> Errors => _errors;

        private void OnSkinsManagerCreated(object sender, AcObjectEventArgs<CarSkinObject> args) {
            _errors.Add(args.AcObject.Errors);
            args.AcObject.AcObjectOutdated += OnAcObjectOutdated;
        }

        private void OnAcObjectOutdated(object sender, EventArgs e) {
            var ac = (AcCommonObject)sender;
            ac.AcObjectOutdated -= OnAcObjectOutdated;
            _errors.Remove(ac.Errors);
        }
        #endregion

        [NotNull]
        public CarSkinsManager SkinsManager { get; }

        [NotNull]
        public AcEnabledOnlyCollection<CarSkinObject> EnabledOnlySkins => SkinsManager.Enabled;

        /* TODO: force sorting by ID! */

        [CanBeNull]
        private CarSkinObject _selectedSkin;

        private bool _selectedSkinSet;

        [CanBeNull]
        public CarSkinObject SelectedSkin {
            get {
                if (!_selectedSkinSet) {
                    _selectedSkinSet = true;
                    if (!SkinsManager.IsScanned) {
                        SkinsManager.Scan();
                    }
                    SelectPreviousOrDefaultSkin();
                }
                return _selectedSkin;
            }
            set {
                if (Equals(value, _selectedSkin)) return;
                _selectedSkin = value;
                OnPropertyChanged(nameof(SelectedSkin));
                OnPropertyChanged(nameof(SelectedSkinLazy));

                if (_selectedSkin == null) return;
                if (_selectedSkin.Id == SkinsManager.WrappersList.FirstOrDefault()?.Value.Id) {
                    LimitedStorage.Remove(LimitedSpace.SelectedSkin, Id);
                } else {
                    LimitedStorage.Set(LimitedSpace.SelectedSkin, Id, _selectedSkin.Id);
                }
            }
        }

        public CarSkinObject SelectedSkinLazy {
            get {
                if (!_selectedSkinSet) {
                    _selectedSkinSet = true;
                    if (!SkinsManager.IsScanned) {
                        Task.Run(() => {
                            SkinsManager.Scan();
                            SelectPreviousOrDefaultSkin();
                        });
                    } else {
                        Task.Run(() => SelectPreviousOrDefaultSkin());
                    }
                }
                return _selectedSkin;
            }
        }

        private void SelectPreviousOrDefaultSkin() {
            var selectedSkinId = LimitedStorage.Get(LimitedSpace.SelectedSkin, Id);
            _selectedSkin = (selectedSkinId == null ? null : SkinsManager.GetById(selectedSkinId)) ?? SkinsManager.GetDefault();
            OnPropertyChanged(nameof(SelectedSkin));
            OnPropertyChanged(nameof(SelectedSkinLazy));
        }

        void IAcManagerScanWrapper.AcManagerScan() {
            var m = Measure("Scanning skins…");
            ClearErrors(AcErrorCategory.CarSkins);

            try {
                SkinsManager.ActualScan();
                RemoveError(AcErrorType.CarSkins_DirectoryIsUnavailable);
            } catch (IOException e) {
                AddError(AcErrorType.CarSkins_DirectoryIsUnavailable, e);
                Logging.Write("Car skins unhandled exception: " + e);
                return;
            }

            m?.Step("Skins are scanned");
        }

        [CanBeNull]
        public CarSkinObject GetSkinById([NotNull] string skinId) {
            return SkinsManager.GetById(skinId);
        }

        [CanBeNull]
        public CarSkinObject GetSkinByIdFromConfig([NotNull] string skinId) {
            return string.IsNullOrWhiteSpace(skinId) || skinId == @"-" ? GetFirstSkinOrNull() : GetSkinById(skinId);
        }

        [CanBeNull]
        public CarSkinObject GetFirstSkinOrNull() {
            return SkinsManager.GetFirstOrNull();
        }

        private BetterListCollectionView _enabledSkinsListView;

        public BetterListCollectionView EnabledSkinsListView {
            get {
                if (_enabledSkinsListView != null) return _enabledSkinsListView;
                _enabledSkinsListView = new BetterListCollectionView(SkinsManager.Enabled) { CustomSort = CarSkinComparer.Comparer };
                _enabledSkinsListView.MoveCurrentTo(SelectedSkin);
                _enabledSkinsListView.CurrentChanged += (sender, args) => { SelectedSkin = _enabledSkinsListView.CurrentItem as CarSkinObject; };
                return _enabledSkinsListView;
            }
        }
    }
}