using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class CarObject : IAcManagerScanWrapper {
        /* for UI car's skins manager */
        public CarSkinsManager SkinsManager { get; private set; }

        public IAcWrapperObservableCollection SkinsWrappers => SkinsManager.WrappersList;

        public IEnumerable<CarSkinObject> Skins => SkinsManager.LoadedOnly;

        public AcLoadedOnlyCollection<CarSkinObject> LoadedOnlySkins => SkinsManager.LoadedOnlyCollection;

        /* TODO: force sorting by ID! */

        private CarSkinObject _selectedSkin;

        [CanBeNull]
        public CarSkinObject SelectedSkin {
            get {
                if (!SkinsManager.IsScanned) {
                    SkinsManager.Scan();
                }
                return _selectedSkin;
            }
            set {
                if (value == _selectedSkin) return;
                _selectedSkin = value;
                OnPropertyChanged(nameof(SelectedSkin));

                if (_selectedSkin == null) return;
                if (_selectedSkin.Id == SkinsWrappers.FirstOrDefault()?.Value.Id) {
                    ValuesStorage.Remove("__carobject_selectedskin_" + Id);
                } else {
                    ValuesStorage.Set("__carobject_selectedskin_" + Id, _selectedSkin.Id);
                }
            }
        }

        private void SelectPreviousOrDefaultSkin() {
            var selectedSkinId = ValuesStorage.GetString("__carobject_selectedskin_" + Id);
            SelectedSkin = (selectedSkinId == null ? null : SkinsManager.GetById(selectedSkinId)) ?? SkinsManager.GetDefault();
        }

        void IAcManagerScanWrapper.AcManagerScan() {
            ClearErrors(AcErrorCategory.CarSkins);

            try {
                SkinsManager.ActualScan();
            } catch (IOException e) {
                AddError(AcErrorType.CarSkins_DirectoryIsUnavailable, e);
                Logging.Write("Car skins unhandled exception: " + e);
                return;
            }

            if (!SkinsWrappers.Any()) {
                AddError(!Directory.Exists(SkinsDirectory) ? AcErrorType.CarSkins_DirectoryIsMissing :
                        AcErrorType.CarSkins_SkinsAreMissing);
            }

            SelectPreviousOrDefaultSkin();
        }

        public void EnsureSkinsLoaded() {
            SkinsManager.EnsureLoaded();
        }

        public async Task EnsureSkinsLoadedAsync() {
            await SkinsManager.EnsureLoadedAsync();
        }

        public bool IsSkinsScanned => SkinsManager.IsScanned;

        public bool IsSkinsLoaded => SkinsManager.IsLoaded;

        [CanBeNull]
        public CarSkinObject GetSkinById([NotNull]string skinId) {
            return SkinsManager.GetById(skinId);
        }

        [CanBeNull]
        public CarSkinObject GetSkinByIdFromConfig([NotNull]string skinId) {
            return string.IsNullOrWhiteSpace(skinId) || skinId == "-" ? GetFirstSkinOrNull() : GetSkinById(skinId);
        }

        [CanBeNull]
        public CarSkinObject GetFirstSkinOrNull() {
            return SkinsManager.GetFirstOrNull();
        }
    }
}
