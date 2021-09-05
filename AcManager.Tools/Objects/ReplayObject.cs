using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ReplayObject : AcCommonSingleFileObject {
        public static string PreviousReplayName { get; } = @"cr";
        public static string AutosaveCategory { get; } = @"temp";
        public const string ReplayExtension = ".acreplay";

        public override string Extension => ReplayExtension;

        [CanBeNull]
        public string Category { get; }

        private string _editableCategory;

        [CanBeNull]
        public string EditableCategory {
            get => _editableCategory;
            set {
                value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (Equals(value, _editableCategory)) return;
                _editableCategory = value;
                OnPropertyChanged();
                if (value != Category) {
                    Changed = true;
                }
            }
        }

        protected override DateTime GetCreationDateTime() {
            return File.GetLastWriteTime(Location);
        }

        public ReplayObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            Category = Path.GetDirectoryName(id);
            EditableCategory = Category;
            if (string.IsNullOrWhiteSpace(Category)) Category = null;
        }

        protected override string GetNewId(string newName) {
            var fileName = SettingsHolder.Drive.AutoAddReplaysExtension ? newName + Extension : newName;
            return EditableCategory == null ? fileName : Path.Combine(EditableCategory, fileName);
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase)) {
                Reload();
            }

            return true;
        }

        public bool IsAutoSave => !AcSettingsHolder.Replay.Autosave
                ? Category == null && Id == PreviousReplayName : string.Equals(Category, AutosaveCategory, StringComparison.OrdinalIgnoreCase);

        public override string Name {
            get => base.Name;
            protected set {
                ErrorIf(value == null || value.Contains("[") || value.Contains("]"), AcErrorType.Replay_InvalidName);
                base.Name = value;
            }
        }

        public override Task SaveAsync() {
            if (EditableCategory != Category) {
                RenameAsync().Ignore();
            }

            return base.SaveAsync();
        }

        /*public async Task ReplaceWeather(string newWeatherId) {
            var temporary = FileUtils.EnsureUnique(Path.Combine(Path.GetDirectoryName(Location) ?? "", "tmp"));
            using (var reader = new ReplayReader(Location)) {
                var version = reader.ReadInt32();
                if (version < 14) {
                    throw new Exception("Unsupported version of replay");
                }

                var something = reader.ReadBytes(8);
                reader.ReadString();

                using (var stream = File.Open(temporary, FileMode.CreateNew, FileAccess.ReadWrite))
                using (var updated = new BinaryWriter(File.Open(temporary, FileMode.CreateNew, FileAccess.ReadWrite), Encoding.ASCII, true)) {
                    updated.Write(version);
                    updated.Write(something);
                    updated.Write(newWeatherId.Length);
                    updated.Write(Encoding.ASCII.GetBytes(newWeatherId));
                    await reader.CopyToAsync(stream).ConfigureAwait(false);
                }

                FileUtils.Recycle(Location);
                File.Move(temporary, Location);
            }
        }*/

        protected override void LoadOrThrow() {
            EditableCategory = Category;
            OldName = Path.GetFileName(Id).ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            Name = OldName;

            var fileInfo = new FileInfo(Location);
            if (!fileInfo.Exists) {
                throw new AcErrorException(this, AcErrorType.Load_Base, "File appears to be deleted");
            }

            Size = fileInfo.Length;

            if (Id == PreviousReplayName) {
                IsNew = true;
            }

            if (SettingsHolder.Drive.TryToLoadReplays) {
                var details = ReplayDetails.Load(Location);
                if (details != null && details.ParseError == null) {
                    ParsedSuccessfully = true;
                    WeatherId = details.WeatherId;
                    TrackId = details.TrackId;
                    CarId = details.CarId;
                    DriverName = details.DriverName;
                    NationCode = details.NationCode;
                    DriverTeam = details.DriverTeam;
                    CarSkinId = details.CarSkinId;
                    TrackConfiguration = details.TrackConfiguration;
                    RaceIniConfig = details.RaceIniConfig;
                    RecordingIntervalMs = details.RecordingIntervalMs;
                    SunAngleFrom = details.SunAngleFrom;
                    SunAngleTo = details.SunAngleTo;
                    CustomTime = details.CustomTime;
                    Version = details.Version;
                    CarsNumber = details.CarsNumber;
                    NumberOfFrames = details.NumberOfFrames;
                    AllowToOverrideTime = details.AllowToOverrideTime;
                } else {
                    ParsedSuccessfully = false;
                    if (details?.ParseError != null) {
                        throw new AcErrorException(this, AcErrorType.Load_Base, details.ParseError);
                    }
                }
            }
        }

        public override string DisplayName => IsAutoSave && Name == PreviousReplayName
                ? ToolsStrings.ReplayObject_PreviousSession
                : base.DisplayName;

        public override int CompareTo(AcPlaceholderNew o) {
            var or = o as ReplayObject;

            var r = o as ReplayObject;
            if (r == null) return base.CompareTo(o);

            var tp = Category;
            var rp = r.Category;

            if (tp != rp) {
                if (tp == AutosaveCategory) return -1;
                if (rp == AutosaveCategory) return 1;
                if (tp == null) return 1;
                if (rp == null) return -1;
                return AlphanumComparatorFast.Compare(tp, rp);
            }

            return CreationDateTime.CompareTo(or.CreationDateTime);
        }

        #region Simple Properties
        public override bool HasData => ParsedSuccessfully;

        private bool _parsedSuccessfully;

        public bool ParsedSuccessfully {
            get => _parsedSuccessfully;
            set => Apply(value, ref _parsedSuccessfully, () => OnPropertyChanged(nameof(HasData)));
        }

        private string _weatherId;

        public string WeatherId {
            get => _weatherId;
            set => Apply(value, ref _weatherId);
        }

        private string _raceIniConfig;

        public string RaceIniConfig {
            get => _raceIniConfig;
            set => Apply(value, ref _raceIniConfig);
        }

        private string _carId;

        public string CarId {
            get => _carId;
            set => Apply(value, ref _carId);
        }

        private string _carSkinId;

        public string CarSkinId {
            get => _carSkinId;
            set => Apply(value, ref _carSkinId);
        }

        private string _driverName;

        public string DriverName {
            get => _driverName;
            set => Apply(value, ref _driverName);
        }

        private string _trackId;

        public string TrackId {
            get => _trackId;
            set => Apply(value, ref _trackId);
        }

        private string _trackConfiguration;

        public string TrackConfiguration {
            get => _trackConfiguration;
            set => Apply(value, ref _trackConfiguration);
        }

        private long _size;

        public long Size {
            get => _size;
            set => Apply(value, ref _size);
        }

        private int _version;

        public int Version {
            get => _version;
            set => Apply(value, ref _version);
        }
        #endregion

        #region Extra values for v16 replays
        private string _nationCode;

        public string NationCode {
            get => _nationCode;
            set => Apply(value, ref _nationCode);
        }

        private string _driverTeam;

        public string DriverTeam {
            get => _driverTeam;
            set => Apply(value, ref _driverTeam);
        }

        private bool _allowToOverrideTime;

        public bool AllowToOverrideTime {
            get => _allowToOverrideTime;
            set => Apply(value, ref _allowToOverrideTime);
        }

        private int? _customTime;

        public int? CustomTime {
            get => _customTime;
            set => Apply(value, ref _customTime);
        }

        private int? _carsNumber;

        public int? CarsNumber {
            get => _carsNumber;
            set => Apply(value, ref _carsNumber);
        }

        private float? _sunAngleFrom;

        public float? SunAngleFrom {
            get => _sunAngleFrom;
            set => Apply(value, ref _sunAngleFrom, () => {
                _timeRange = null;
                OnPropertyChanged(nameof(TimeFrom));
                OnPropertyChanged(nameof(TimeRange));
            });
        }

        private float? _sunAngleTo;

        public float? SunAngleTo {
            get => _sunAngleTo;
            set => Apply(value, ref _sunAngleTo, () => {
                _timeRange = null;
                OnPropertyChanged(nameof(TimeTo));
                OnPropertyChanged(nameof(TimeRange));
            });
        }

        public int? TimeFrom => _sunAngleFrom.HasValue ? Game.ConditionProperties.GetSeconds(_sunAngleFrom.Value).RoundToInt() : (int?)null;
        public int? TimeTo => _sunAngleTo.HasValue ? Game.ConditionProperties.GetSeconds(_sunAngleTo.Value).RoundToInt() : (int?)null;

        private string _timeRange;

        public string TimeRange {
            get {
                if (CustomTime.HasValue) {
                    return CustomTime.Value.ToDisplayTime();
                }

                if (_timeRange == null && TimeFrom.HasValue && TimeTo.HasValue) {
                    var fromSeconds = TimeFrom.Value;
                    var toSeconds = TimeTo.Value;
                    var from = fromSeconds.ToDisplayTime();
                    var to = toSeconds.ToDisplayTime();
                    _timeRange = from == to ? from : $@"{from}–{to}";
                }
                return _timeRange;
            }
        }

        private double? _recordingIntervalMs;

        public double? RecordingIntervalMs {
            get => _recordingIntervalMs;
            set => Apply(value, ref _recordingIntervalMs, () => {
                _duration = null;
                OnPropertyChanged(nameof(DisplayDuration));
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(RecordingQuality));
            });
        }

        public double? RecordingQuality => (1000d / RecordingIntervalMs)?.Floor();

        private int? _numberOfFrames;

        public int? NumberOfFrames {
            get => _numberOfFrames;
            set => Apply(value, ref _numberOfFrames, () => {
                _duration = null;
                OnPropertyChanged(nameof(DisplayDuration));
                OnPropertyChanged(nameof(Duration));
            });
        }

        private TimeSpan? _duration;

        public TimeSpan? Duration => _duration;

        public string DisplayDuration {
            get {
                if (!_duration.HasValue && _recordingIntervalMs.HasValue && _numberOfFrames.HasValue) {
                    _duration = TimeSpan.FromSeconds(_numberOfFrames.Value * _recordingIntervalMs.Value / 1e3);
                }
                return _duration?.ToReadableTime();
            }
        }
        #endregion

        // Bunch of temporary fields for filtering

        private CarObject _car;

        [CanBeNull]
        internal CarObject Car => _car ?? (_car = CarId == null ? null : CarsManager.Instance.GetById(CarId));

        private CarSkinObject _carSkin;

        [CanBeNull]
        internal CarSkinObject CarSkin => _carSkin ?? (_carSkin = CarSkinId == null ? null : Car?.GetSkinById(CarSkinId));

        private WeatherObject _weather;

        [CanBeNull]
        internal WeatherObject Weather => _weather ?? (_weather = WeatherId == null ? null : WeatherManager.Instance.GetById(WeatherId));

        private TrackObjectBase _track;

        [CanBeNull]
        internal TrackObjectBase Track => _track ?? (_track = TrackId == null ? null : TrackConfiguration == null
                ? TracksManager.Instance.GetById(TrackId) : TracksManager.Instance.GetLayoutById(TrackId, TrackConfiguration));
    }
}