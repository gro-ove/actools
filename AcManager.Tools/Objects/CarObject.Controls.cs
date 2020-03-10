using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers.Presets;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private string _keySpecificControlsPreset, _keyControlsPresetFilename;
        private string _keySpecificFanatecSettings, _keySpecificFanatecDisable, _keySpecificFanatecGearOnly;

        private void InitializeControlsKeys() {
            if (_keySpecificControlsPreset != null) return;
            _keySpecificControlsPreset = $@"CarObject:{Id}:SpecificControlsPreset";
            _keyControlsPresetFilename = $@"CarObject:{Id}:ControlsPresetFilename";
            _keySpecificFanatecSettings = $@"CarObject:{Id}:FanatecSettings";
            _keySpecificFanatecDisable = $@"CarObject:{Id}:FanatecDisable";
            _keySpecificFanatecGearOnly = $@"CarObject:{Id}:FanatecGearOnly";
        }

        private bool? _specificControlsPreset;

        public bool SpecificControlsPreset {
            get {
                InitializeControlsKeys();
                return _specificControlsPreset ?? (_specificControlsPreset = ValuesStorage.Get(_keySpecificControlsPreset, false)).Value;
            }
            set {
                if (Equals(value, SpecificControlsPreset)) return;
                InitializeControlsKeys();

                _specificControlsPreset = value;
                OnPropertyChanged();
                ValuesStorage.Set(_keySpecificControlsPreset, value);
            }
        }

        private string _currentControlsPresetName;

        [CanBeNull]
        public string CurrentControlsPresetName {
            get {
                if (!_controlsPresetFilenameLoaded) {
                    InitializeControlsKeys();
                    _controlsPresetFilenameLoaded = true;
                    _controlsPresetFilename = ValuesStorage.Get<string>(_keyControlsPresetFilename);
                    _currentControlsPresetName = _controlsPresetFilename == null ? null : ControlsSettings.GetCurrentPresetName(_controlsPresetFilename);
                }

                return _currentControlsPresetName;
            }
            private set => Apply(value, ref _currentControlsPresetName);
        }

        private string _controlsPresetFilename;
        private bool _controlsPresetFilenameLoaded;

        [CanBeNull]
        public string ControlsPresetFilename {
            get {
                if (!_controlsPresetFilenameLoaded) {
                    InitializeControlsKeys();
                    _controlsPresetFilenameLoaded = true;
                    _controlsPresetFilename = ValuesStorage.Get<string>(_keyControlsPresetFilename);
                }

                return _controlsPresetFilename;
            }
            set {
                if (Equals(value, ControlsPresetFilename)) return;
                InitializeControlsKeys();

                _controlsPresetFilename = value;
                CurrentControlsPresetName = value == null ? null : ControlsSettings.GetCurrentPresetName(value);
                OnPropertyChanged();
                ValuesStorage.Set(_keyControlsPresetFilename, value);
            }
        }

        public object SelectedControlsPreset {
            get => null;
            set {
                if (value is ISavedPresetEntry entry) {
                    ControlsPresetFilename = entry.VirtualFilename;
                }
            }
        }

        private bool? _fanatecCustomSettings;

        public bool FanatecCustomSettings {
            get {
                InitializeControlsKeys();
                return _fanatecCustomSettings
                        ?? (_fanatecCustomSettings = ValuesStorage.Get(_keySpecificFanatecSettings, AcSettingsHolder.Fanatec.GuessPerCar)).Value;
            }
            set => Apply(value, ref _fanatecCustomSettings, () => {
                InitializeControlsKeys();
                ValuesStorage.Set(_keySpecificFanatecSettings, value);
            });
        }

        private bool ShouldFanatecLedsBeDisabled() {
            return Year < 1990 || CarClass == "street" && SpecsPwRatioValue < 0.4;
        }

        private bool? _fanatecDisable;

        public bool FanatecDisable {
            get {
                InitializeControlsKeys();
                return _fanatecDisable
                        ?? (_fanatecDisable = ValuesStorage.Get(_keySpecificFanatecDisable, ShouldFanatecLedsBeDisabled())).Value;
            }
            set => Apply(value, ref _fanatecDisable, () => {
                InitializeControlsKeys();
                ValuesStorage.Set(_keySpecificFanatecDisable, value);
            });
        }

        private bool? _fanatecOnlyShowGear;

        public bool FanatecOnlyShowGear {
            get {
                InitializeControlsKeys();
                return _fanatecOnlyShowGear
                        ?? (_fanatecOnlyShowGear = ValuesStorage.Get(_keySpecificFanatecGearOnly, false)).Value;
            }
            set => Apply(value, ref _fanatecOnlyShowGear, () => {
                InitializeControlsKeys();
                ValuesStorage.Set(_keySpecificFanatecGearOnly, value);
            });
        }
    }
}