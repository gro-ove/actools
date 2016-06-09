using System;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class CarSetupObject : AcCommonSingleFileObject {
        public const string GenericDirectory = "generic";

        public string CarId { get; }

        [CanBeNull]
        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                Track = value == null ? null : TracksManager.Instance.GetById(value);
                ErrorIf(value != null && Track == null, AcErrorType.CarSetup_TrackIsMissing, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                Changed = true;
            }
        }

        [CanBeNull]
        public TrackObject Track {
            get { return _track; }
            set {
                if (Equals(value, _track)) return;
                _track = value;
                TrackId = value?.Id;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                Changed = true;
            }
        }

        public override string Extension => ".ini";

        public CarSetupObject(string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;

            foreach (var tyrePressure in TyresPressure) {
                tyrePressure.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(tyrePressure.Value)) {
                        Changed = true;
                    }
                };
            }
        }

        public override int CompareTo(AcPlaceholderNew o) {
            var c = o as CarSetupObject;
            if (c == null) return base.CompareTo(o);

            var lhsEnabled = Enabled;
            if (lhsEnabled != c.Enabled) return lhsEnabled ? -1 : 1;

            var lhsParent = TrackId;
            var rhsParent = c.TrackId;

            if (lhsParent == null && rhsParent == null || lhsParent == rhsParent) {
                return DisplayName.InvariantCompareTo(c.DisplayName);
            }
            
            return lhsParent.InvariantCompareTo(rhsParent);
        }

        private int _tyres;

        public int Tyres {
            get { return _tyres; }
            set {
                value = Math.Max(value, 0);
                if (Equals(value, _tyres)) return;
                _tyres = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private int _fuel;

        public int Fuel {
            get { return _fuel; }
            set {
                value = Math.Max(value, 0);
                if (Equals(value, _fuel)) return;
                _fuel = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private int _fuelMaximum = int.MaxValue;
        private TrackObject _track;

        public int FuelMaximum {
            get { return _fuelMaximum; }
            set {
                value = Math.Max(value, 0);
                if (Equals(value, _fuelMaximum)) return;
                _fuelMaximum = value;
                OnPropertyChanged();
            }
        }

        public sealed class TyrePressure : Displayable, IWithId<string> {
            public string Id { get; }

            public TyrePressure(string id, string name) {
                Id = id;
                DisplayName = name;
            }

            private int? _value;

            public int? Value {
                get { return _value; }
                set {
                    value = value?.Clamp(0, 200);
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public TyrePressure[] TyresPressure { get; } = {
            new TyrePressure("LF", "Left front"),
            new TyrePressure("RF", "Right front"),
            new TyrePressure("LR", "Left rear"),
            new TyrePressure("RR", "Right rear")
        };

        private string _oldName;
        private string _oldTrackId;

        private void LoadData() {
            try {
                var ini = new IniFile(Location);
                Tyres = ini["TYRES"].GetInt("VALUE", 0);
                Fuel = ini["FUEL"].GetInt("VALUE", 0);
                SetHasData(true);

                foreach (var entry in TyresPressure) {
                    entry.Value = ini["PRESSURE_" + entry.Id].GetIntNullable("VALUE");
                }
            } catch (Exception e) {
                Logging.Warning("[CarSetupObject] Can’t read file: " + e);
                AddError(AcErrorType.Data_IniIsDamaged, Id);
                SetHasData(false);
            }
        }

        protected override void LoadOrThrow() {
            _oldName = Path.GetFileName(Id.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase));
            Name = _oldName;

            var dir = Path.GetDirectoryName(Id);
            if (string.IsNullOrEmpty(dir)) {
                AddError(AcErrorType.Load_Base, "Invalid location");
                _oldTrackId = null;
                TrackId = _oldTrackId;
                SetHasData(false);
                return;
            }

            _oldTrackId = dir == GenericDirectory ? null : dir;
            TrackId = _oldTrackId;
            LoadData();
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase)) {
                LoadData();
            }

            return true;
        }

        protected override void Rename() {
            Rename(Path.Combine(Track?.Id ?? GenericDirectory, Name + Extension));
        }

        public override void Save() {
            try {
                var ini = new IniFile(Location);
                ini["TYRES"].Set("VALUE", Tyres);
                ini["FUEL"].Set("VALUE", Fuel);

                foreach (var entry in TyresPressure) {
                    ini["PRESSURE_" + entry.Id].Set("VALUE", entry.Value);
                }

                ini.SaveAs(Location);
            } catch (Exception e) {
                Logging.Warning("[CarSetupObject] Can’t save file: " + e);
            }

            if (_oldName != Name || _oldTrackId != TrackId) {
                Rename();
            }

            Changed = false;
        }

        public override string DisplayName => TrackId == null ? Name : $"{Name} ({Track?.DisplayName ?? TrackId})";

        private bool _hasData;
        private string _trackId;

        private void SetHasData(bool value) {
            if (Equals(value, _hasData)) return;
            _hasData = value;
            OnPropertyChanged(nameof(HasData));
        }

        public override bool HasData => _hasData;
    }
}
