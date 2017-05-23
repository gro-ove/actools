using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public static readonly string FileExtension = ".ini";

        public override string Extension => FileExtension;

        public CarSetupObject(string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;
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

        private TrackObject _track;

        private string _oldName;
        private string _oldTrackId;

        private IniFile _iniFile;

        public IEnumerable<KeyValuePair<string, double?>> Values =>
                _iniFile.Select(x => new KeyValuePair<string, double?>(x.Key, x.Value.GetDoubleNullable("VALUE")));

        public double? GetValue(string key) {
            if (!_iniFile.ContainsKey(key)) {
                Logging.Warning($"Key not found: {key}");
            }
            return _iniFile[key].GetDoubleNullable("VALUE");
        }

        public void SetValue(string key, double value) {
            var rounded = value.RoundToInt();
            if (GetValue(key) == rounded) return;
            _iniFile[key].Set("VALUE", value.RoundToInt());
            OnPropertyChanged(nameof(Values));
            Changed = true;
        }

        private void LoadData() {
            try {
                _iniFile = new IniFile(Location);
                Tyres = _iniFile["TYRES"].GetInt("VALUE", 0);
                RemoveError(AcErrorType.Data_IniIsDamaged);
                SetHasData(true);
            } catch (Exception e) {
                _iniFile = new IniFile();
                Logging.Warning("Can’t read file: " + e);
                AddError(AcErrorType.Data_IniIsDamaged, Id);
                SetHasData(false);
            }

            Changed = false;
            OnPropertyChanged(nameof(Values));
        }

        protected override void LoadOrThrow() {
            _oldName = Path.GetFileName(Id.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase));
            Name = _oldName;

            var dir = Path.GetDirectoryName(Id);
            if (string.IsNullOrEmpty(dir)) {
                AddError(AcErrorType.Load_Base, ToolsStrings.CarSetupObject_Load_InvalidLocation);
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
                for (var i = 0; i < 4; i++) {
                    try {
                        LoadData();
                        break;
                    } catch (IOException e) {
                        Logging.Warning(e);
                        Thread.Sleep(100);
                    }
                }
            }

            return true;
        }

        protected override string GetNewId(string newName) {
            return Path.Combine(Track?.Id ?? GenericDirectory, newName + Extension);
        }

        public override void Save() {
            try {
                if (_iniFile == null) {
                    _iniFile = new IniFile(Location);
                }

                _iniFile["TYRES"].Set("VALUE", Tyres);
                _iniFile.Save(Location);
            } catch (Exception e) {
                Logging.Warning("Can’t save file: " + e);
            }

            if (_oldName != Name || _oldTrackId != TrackId) {
                RenameAsync();
            }

            Changed = false;
        }

        public override string DisplayName => TrackId == null ? Name : $"{Name} ({Track?.MainTrackObject.NameEditable ?? TrackId})";

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
