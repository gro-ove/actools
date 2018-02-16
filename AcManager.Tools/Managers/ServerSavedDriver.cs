using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public sealed class ServerSavedDriver : NotifyPropertyErrorsChanged, IDraggable, IDraggableCloneable {
        public override IEnumerable GetErrors(string propertyName) {
            switch (propertyName) {
                case nameof(Guid):
                    return new[] { "GUID is required" };
                case nameof(DriverName):
                    return new[] { "Driver name is required" };
                default:
                    return null;
            }
        }

        public override bool HasErrors => Guid == string.Empty || DriverName == string.Empty;

        private string _guid;

        [NotNull]
        public string Guid {
            get => _guid;
            set {
                value = value.Trim();
                if (value == _guid) return;
                _guid = value;
                OnPropertyChanged();
                OnErrorsChanged();
            }
        }

        private string _driverName;

        [NotNull]
        public string DriverName {
            get => _driverName;
            set {
                value = value.Trim();
                if (value == _driverName) return;
                _driverName = value;
                OnPropertyChanged();
                OnErrorsChanged();
            }
        }

        private string _teamName;

        [CanBeNull]
        public string TeamName {
            get => _teamName;
            set => Apply(value, ref _teamName);
        }

        [NotNull]
        public IReadOnlyDictionary<string, string> Skins { get; }

        [CanBeNull]
        public string GetCarId() {
            return Skins.FirstOrDefault().Key;
        }

        [CanBeNull]
        public string GetSkinId([NotNull] string carId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));
            return Skins.FirstOrDefault(x => string.Equals(x.Key, carId, StringComparison.OrdinalIgnoreCase)).Value;
        }

        private ServerSavedDriver() {
            Skins = new Dictionary<string, string>();
        }

        private ServerSavedDriver(KeyValuePair<string, IniFileSection> pair) {
            Guid = pair.Key.ApartFromFirst(@"D");
            DriverName = pair.Value.GetNonEmpty("DRIVERNAME") ?? "Unnamed";
            TeamName = pair.Value.GetNonEmpty("TEAM");
            Skins = pair.Value.Where(x => x.Key != @"DRIVERNAME" && x.Key != @"TEAM").ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Value);
        }

        public void Save(IniFile file) {
            var section = file[@"D" + (string.IsNullOrWhiteSpace(Guid) ? "0" : Guid)];
            section.Set("DRIVERNAME", string.IsNullOrWhiteSpace(DriverName) ? "Unnamed" : DriverName);
            section.Set("TEAM", TeamName);
            foreach (var pair in Skins) {
                section.Set(pair.Key.ToUpperInvariant(), pair.Value);
            }
        }

        public bool CanBeCloned => true;

        public object Clone() {
            return new ServerSavedDriver {
                Guid = Guid,
                DriverName = DriverName,
                TeamName = TeamName
            };
        }

        internal ServerSavedDriver(ServerPresetDriverEntry driverEntry) {
            if (driverEntry.Guid == null || driverEntry.DriverName == null) {
                throw new Exception("GUID and name are required");
            }

            Guid = driverEntry.Guid;
            DriverName = driverEntry.DriverName;
            TeamName = driverEntry.TeamName;

            if (driverEntry.CarSkinId != null) {
                Skins = new Dictionary<string, string> {
                    [driverEntry.CarId] = driverEntry.CarSkinId
                };
            } else {
                Skins = new Dictionary<string, string>(0);
            }
        }

        private bool Equals(ServerSavedDriver other) {
            return string.Equals(Guid, other.Guid) && string.Equals(DriverName, other.DriverName) && string.Equals(TeamName, other.TeamName) &&
                    Skins.SequenceEqual(other.Skins);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var a = obj as ServerSavedDriver;
            return a != null && Equals(a);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Guid.GetHashCode();
                hashCode = (hashCode * 397) ^ DriverName.GetHashCode();
                hashCode = (hashCode * 397) ^ (TeamName?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ Skins.Aggregate(0, (current, skin) => current ^ skin.GetHashCode()).GetHashCode();
                return hashCode;
            }
        }

        public static IEnumerable<ServerSavedDriver> Load(string filename) {
            return File.Exists(filename)
                    ? new IniFile(filename, IniFileMode.ValuesWithSemicolons).Where(x => x.Key.Length > 1 && x.Key[0] == 'D')
                                                                             .Select(x => new ServerSavedDriver(x))
                    : new ServerSavedDriver[0];
        }

        public void Extend(ServerPresetDriverEntry driverEntry) {
            var d = (Dictionary<string, string>)Skins;
            if (driverEntry.CarSkinId != null) {
                d[driverEntry.CarId] = driverEntry.CarSkinId;
            }
        }

        private bool _deleted;

        public bool Deleted {
            get => _deleted;
            set => Apply(value, ref _deleted);
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
        }));

        public const string DraggableFormat = "Data-ServerSavedDriver";

        string IDraggable.DraggableFormat => DraggableFormat;

        public void CopyTo(ServerPresetDriverEntry target) {
            target.DriverName = DriverName;
            target.TeamName = TeamName;
            target.Guid = Guid;
        }
    }
}