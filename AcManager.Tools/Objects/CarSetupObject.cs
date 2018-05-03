using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class CarSetupObject : AcCommonSingleFileObject, ICarSetupObject {
        public const string GenericDirectory = "generic";

        public string CarId { get; }

        public string TrackId {
            get => _trackId;
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

        public TrackObject Track {
            get => _track;
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
            return this.CompareTo(o as ICarSetupObject);
        }

        private int _tyres;

        public int? Tyres {
            get => _tyres;
            set {
                if (!value.HasValue) return;

                value = Math.Max(value.Value, 0);
                if (Equals(value, _tyres)) return;
                _tyres = value.Value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        public Task EnsureDataLoaded() {
            return Task.Delay(0);
        }

        public bool IsReadOnly => false;

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

        public override Task SaveAsync() {
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
                // TODO: Await?
                RenameAsync().Forget();
            }

            Changed = false;
            return Task.Delay(0);
        }

        // public override string DisplayName => TrackId == null ? Name : $"{Name} ({Track?.MainTrackObject.NameEditable ?? TrackId})";

        private bool _hasData;
        private string _trackId;

        private void SetHasData(bool value) {
            if (Equals(value, _hasData)) return;
            _hasData = value;
            OnPropertyChanged(nameof(HasData));
        }

        public override bool HasData => _hasData;

        public static string FixEntryName(string name, bool titleCase) {
            if (string.IsNullOrWhiteSpace(name)) return "?";

            var s = Regex.Replace(name, @"[^a-zA-Z0-9\.,-]+|(?<=[a-z])(?=[A-Z])", " ").Trim().Split(' ');
            var b = new List<string>();
            foreach (var p in s) {
                if (p.Length < 3 && !Regex.IsMatch(p, @"^(?:a|an|as|at|by|en|if|in|of|on|or|the|to|vs)$") ||
                        Regex.IsMatch(p, @"^(?:arb)$")) {
                    b.Add(p.ToUpperInvariant());
                } else {
                    b.Add(titleCase || b.Count == 0 ? char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant() :
                            p.ToLowerInvariant());
                }
            }
            return b.JoinToString(' ');
        }
    }
}