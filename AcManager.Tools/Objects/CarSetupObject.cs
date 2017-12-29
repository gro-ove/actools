using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public interface ICarSetupObject : INotifyPropertyChanged {
        bool Enabled { get; }

        [NotNull]
        string DisplayName { get; }

        [NotNull]
        string CarId { get; }

        [CanBeNull]
        string TrackId { get; }

        [CanBeNull]
        TrackObject Track { get; }

        bool IsReadOnly { get; }

        Task EnsureDataLoaded();

        int? Tyres { get; }

        IEnumerable<KeyValuePair<string, double?>> Values { get; }

        double? GetValue(string key);

        void SetValue(string key, double entryValue);
    }

    public static class CarSetupObjectExtension {
        public static int CompareTo(this ICarSetupObject l, ICarSetupObject r) {
            if (l == null) return r == null ? 0 : 1;
            if (r == null) return -1;

            var lhsEnabled = l.Enabled;
            if (lhsEnabled != r.Enabled) return lhsEnabled ? -1 : 1;

            var lhsParentId = l.TrackId;
            var rhsParentId = r.TrackId;

            if (lhsParentId == null) return rhsParentId == null ? 0 : -1;
            if (rhsParentId == null) return 1;
            if (lhsParentId == rhsParentId) {
                return l.DisplayName.InvariantCompareTo(r.DisplayName);
            }

            var lhsParent = l.Track;
            var rhsParent = r.Track;
            if (lhsParent == null) return rhsParent == null ? 0 : 1;
            return rhsParent == null ? -1 : lhsParent.CompareTo(rhsParent);
        }
    }

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
                RenameAsync().Forget();
            }

            Changed = false;
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
    }

    public class RemoteCarSetupObject : AcObjectNew, ICarSetupObject {
        public CarSetupsRemoteSource Source { get; }

        private readonly RemoteSetupInformation _information;

        public RemoteCarSetupObject(RemoteSetupsManager manager, RemoteSetupInformation information) : base(manager, information.Id, true) {
            _information = information;
            Source = manager.Source;
            CarId = _information.CarId;
            TrackId = _information.TrackKunosId;
            Author = _information.Author;
            Version = _information.Version;
            Downloads = _information.Downloads;
            CommunityRating = _information.CommunityRating;
            Trim = _information.Trim;
            BestTime = _information.BestTime;
            _track = new Lazy<TrackObject>(() => TrackId == null ? null : TracksManager.Instance.GetLayoutByKunosId(TrackId)?.MainTrackObject);
        }

        public override void Load() {
            Name = _information.FileName.ApartFromLast(CarSetupObject.FileExtension, StringComparison.OrdinalIgnoreCase);
            CreationDateTime = _information.AddedDateTime ?? DateTime.Now - TimeSpan.FromDays(1e3);
        }

        private readonly Lazy<TrackObject> _track;
        public TrackObject Track => _track.Value;

        public string CarId { get; }
        public string TrackId { get; }
        public string Author { get; }
        public string Version { get; }
        public int Downloads { get; }
        public double? CommunityRating { get; }
        public string Trim { get; }
        public TimeSpan? BestTime { get; }

        public bool IsReadOnly => true;

        private string _loadedData;

        private async Task EnsureDataLoadedInner() {
            try {
                _loadedData = await TheSetupMarketApiProvider.GetSetup(_information.Id) ?? "";
                _iniFile = IniFile.Parse(_loadedData);

                Tyres = _iniFile["TYRES"].GetInt("VALUE", 0);
                Logging.Debug(Tyres);
                OnPropertyChanged(nameof(Values));
            } finally {
                _loaded = true;
                _loadingTask = null;
            }
        }

        public override int CompareTo(AcPlaceholderNew o) {
            return this.CompareTo(o as ICarSetupObject);
        }

        private bool _loaded;
        private Task _loadingTask;

        public Task EnsureDataLoaded() {
            if (_loaded) return Task.Delay(0);
            if (_loadingTask != null) return _loadingTask;
            return _loadingTask = EnsureDataLoadedInner();
        }

        private int? _tyres;

        public int? Tyres {
            get => _tyres;
            set {
                if (value == _tyres) return;
                _tyres = value;
                OnPropertyChanged();
            }
        }

        private IniFile _iniFile = new IniFile();

        public IEnumerable<KeyValuePair<string, double?>> Values =>
                _iniFile.Select(x => new KeyValuePair<string, double?>(x.Key, x.Value.GetDoubleNullable("VALUE")));

        public double? GetValue(string key) {
            if (!_loaded) return null;

            if (!_iniFile.ContainsKey(key)) {
                Logging.Warning($"Key not found: {key}");
            }

            return _iniFile[key].GetDoubleNullable("VALUE");
        }

        public void SetValue(string key, double entryValue) {}

        private DelegateCommand _viewInBrowserCommand;

        public DelegateCommand ViewInBrowserCommand => _viewInBrowserCommand ?? (_viewInBrowserCommand = new DelegateCommand(() => {
            WindowsHelper.ViewInBrowser(_information.Url);
        }, () => _information.Url != null));

        private DelegateCommand _copyUrlCommand;

        public DelegateCommand CopyUrlCommand => _copyUrlCommand ?? (_copyUrlCommand = new DelegateCommand(() => {
            if (_information.Url == null) return;
            Clipboard.SetText(_information.Url);
            Toast.Show("Link copied", "Link to The Setup Market copied to the clipboard");
        }, () => _information.Url != null));

        private AsyncCommand<string> _installCommand;

        public AsyncCommand<string> InstallCommand => _installCommand ?? (_installCommand = new AsyncCommand<string>(async d => {
            await EnsureDataLoaded();
            var filename = FileUtils.EnsureUnique(Path.Combine(AcPaths.GetCarSetupsDirectory(CarId),
                    d ?? (Track?.Id ?? _information.TrackKunosId ?? CarSetupObject.GenericDirectory), _information.FileName));
            FileUtils.EnsureFileDirectoryExists(filename);
            File.WriteAllText(filename, _loadedData);
            Toast.Show("Setup installed", $"Setup {DisplayName} downloaded and installed");
        }));
    }
}
