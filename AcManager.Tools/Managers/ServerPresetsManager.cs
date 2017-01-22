using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class ServerPresetsManager : AcManagerNew<ServerPresetObject>, IDisposable {
        private static ServerPresetsManager _instance;

        public static ServerPresetsManager Instance => _instance ?? (_instance = new ServerPresetsManager());

        private static readonly string[] WatchedFiles = {
            @"server_cfg.ini", @"entry_list.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            return !WatchedFiles.Contains(inner.ToLowerInvariant());
        }

        private static readonly string PresetsDirectory;
        private static readonly string DriversFilename;

        static ServerPresetsManager() {
            PresetsDirectory = Path.Combine(AcRootDirectory.Instance.RequireValue, @"server", @"presets");
            DriversFilename = Path.Combine(AcRootDirectory.Instance.RequireValue, @"server", @"manager", @"driverlist.ini");
            Directory.CreateDirectory(PresetsDirectory);
        }

        private readonly FileSystemWatcher _directoryWatcher;

        public ServerPresetsManager() {
            SavedDrivers = new ChangeableObservableCollection<ServerSavedDriver>(ServerSavedDriver.Load(DriversFilename));
            SavedDrivers.CollectionChanged += OnSavedDriversCollectionChanged;
            SavedDrivers.ItemPropertyChanged += OnSavedDriversItemPropertyChanged;

            var directory = Path.GetDirectoryName(DriversFilename);
            if (directory != null) {
                Directory.CreateDirectory(directory);

                _directoryWatcher = new FileSystemWatcher {
                    Path = directory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = Path.GetFileName(DriversFilename),
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                _directoryWatcher.Created += OnDirectoryWatcher;
                _directoryWatcher.Renamed += OnDirectoryWatcher;
                _directoryWatcher.Changed += OnDirectoryWatcher;
                _directoryWatcher.Deleted += OnDirectoryWatcher;
            }
        }

        private void OnSavedDriversCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            SaveDriversLater().Forget();
        }

        private void OnSavedDriversItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ServerSavedDriver.Deleted)) {
                SavedDrivers.Remove((ServerSavedDriver)sender);
            } else {
                SaveDriversLater().Forget();
            }
        }

        private bool _updating, _saving;

        private async Task SaveDriversLater() {
            if (_updating || _saving) return;
            _saving = true;

            try {
                await Task.Delay(300);
                if (_saving) {
                    SaveDrivers();
                    await Task.Delay(200);
                }
            } finally {
                _saving = false;
            }
        }

        private void SaveDrivers() {
            try {
                var file = new IniFile();
                foreach (var driver in SavedDrivers) {
                    driver.Save(file);
                }
                file.Save(DriversFilename);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t save drivers list", e);
            }
        }

        private async void OnDirectoryWatcher(object sender, FileSystemEventArgs e) {
            if (_updating || _saving) return;
            _updating = true;

            try {
                await Task.Delay(300);
                if (_updating) {
                    ActionExtension.InvokeInMainThread(() => {
                        SavedDrivers.ReplaceIfDifferBy(ServerSavedDriver.Load(DriversFilename));
                    });
                    await Task.Delay(200);
                }
            } finally {
                _updating = false;
            }
        }

        public void StoreDriverEntry(ServerPresetDriverEntry entry) {
            var saved = SavedDrivers.FirstOrDefault(x => x.Guid == entry.Guid);
            if (saved != null) {
                saved.Extend(entry);
                SaveDriversLater().Forget();
            } else {
                SavedDrivers.Add(new ServerSavedDriver(entry));
            }
        }

        public override IAcDirectories Directories { get; } = new AcDirectories(PresetsDirectory);

        protected override ServerPresetObject CreateAcObject(string id, bool enabled) {
            return new ServerPresetObject(this, id, enabled);
        }

        public ChangeableObservableCollection<ServerSavedDriver> SavedDrivers { get; }

        public void Dispose() {
            _directoryWatcher.EnableRaisingEvents = false;
            _directoryWatcher.Dispose();
        }
    }

    public sealed class ServerSavedDriver : NotifyPropertyErrorsChanged, IDraggable {
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
            get { return _guid; }
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
            get { return _driverName; }
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
            get { return _teamName; }
            set {
                if (value == _teamName) return;
                _teamName = value;
                OnPropertyChanged();
            }
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
                hashCode = (hashCode * 397) ^ (DriverName?.GetHashCode() ?? 0);
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

        public const string DraggableFormat = "Data-ServerSavedDriver";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}
