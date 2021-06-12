using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
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

            if (!SettingsHolder.Drive.TryToLoadReplays) return;

            try {
                using (var reader = new ReplayReader(Location)) {
                    var version = reader.ReadInt32();
                    Version = version;

                    if (version == 16) {
                        ParseV16(reader);
                    } else {
                        ParseGeneric(version, reader);
                    }
                }

                ParsedSuccessfully = true;
            } catch (Exception e) {
                ParsedSuccessfully = false;
                throw new AcErrorException(this, AcErrorType.Load_Base, e);
            }
        }

        private bool ReadExtendedSection(ReplayReader reader, string name, int length) {
            if (name == @"CONFIG_RACE") {
                RaceIniConfig = Encoding.ASCII.GetString(reader.ReadBytes(length));
                CustomTime = Game.ConditionProperties.GetSeconds(
                        IniFile.Parse(RaceIniConfig)["LIGHTING"].GetDoubleNullable("__CM_UNCLAMPED_SUN_ANGLE")
                                ?? IniFile.Parse(RaceIniConfig)["LIGHTING"].GetDouble("SUN_ANGLE", 80d)).RoundToInt();
                return true;
            }

            return false;
        }

        private void ParseV16(ReplayReader reader) {
            RecordingIntervalMs = reader.ReadDouble();

            WeatherId = reader.ReadString();
            TrackId = reader.ReadString();
            TrackConfiguration = reader.ReadString();

            CarsNumber = reader.ReadInt32();
            reader.ReadInt32(); // current recording index
            var frames = reader.ReadInt32();
            NumberOfFrames = frames;

            var trackObjectsNumber = reader.ReadInt32();
            var minSunAngle = default(float?);
            var maxSunAngle = default(float?);
            for (var i = 0; i < frames; i++) {
                float sunAngle = reader.ReadHalf();
                reader.Skip(2 + trackObjectsNumber * 12);
                if (!minSunAngle.HasValue) minSunAngle = sunAngle;
                maxSunAngle = sunAngle;
            }

            if (minSunAngle.HasValue
                    && Game.ConditionProperties.GetSeconds(minSunAngle.Value) > Game.ConditionProperties.GetSeconds(maxSunAngle.Value)) {
                SunAngleFrom = maxSunAngle;
                SunAngleTo = minSunAngle;
            } else {
                SunAngleFrom = minSunAngle;
                SunAngleTo = maxSunAngle;
            }

            CarId = reader.ReadString();
            DriverName = reader.ReadString();
            NationCode = reader.ReadString();
            DriverTeam = reader.ReadString();
            CarSkinId = reader.ReadString();

            const string postfix = "__AC_SHADERS_PATCH_v1__";
            reader.Seek(-postfix.Length - 8, SeekOrigin.End);
            if (Encoding.ASCII.GetString(reader.ReadBytes(postfix.Length)) == postfix) {
                var start = reader.ReadUInt32();
                var version = reader.ReadUInt32();
                if (version == 1) {
                    reader.Seek(start, SeekOrigin.Begin);

                    while (true) {
                        var nameLength = reader.ReadInt32();
                        if (nameLength > 255) break;

                        var name = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
                        // Logging.Debug("Extra section: " + name);

                        var sectionLength = reader.ReadInt32();
                        if (!ReadExtendedSection(reader, name, sectionLength)) {
                            reader.Skip(sectionLength);
                        }
                    }
                }
            }

            AllowToOverrideTime = CustomTime == null && WeatherManager.Instance.GetById(WeatherId)?.IsWeatherTimeUnusual() == true;
        }

        private void ParseGeneric(int version, ReplayReader reader) {
            AllowToOverrideTime = false;

            if (version >= 14) {
                reader.Skip(8);

                WeatherId = reader.ReadString();
                /*if (!string.IsNullOrWhiteSpace(WeatherId)) {
                            ErrorIf(WeatherManager.Instance.GetWrapperById(WeatherId) == null,
                                    AcErrorType.Replay_WeatherIsMissing, WeatherId);
                        }*/

                TrackId = reader.ReadString();
                TrackConfiguration = reader.ReadString();
            } else {
                TrackId = reader.ReadString();
            }

            ErrorIf(TracksManager.Instance.GetWrapperById(TrackId) == null,
                    AcErrorType.Replay_TrackIsMissing, TrackId);

            CarId = reader.TryToReadNextString();

            if (!string.IsNullOrWhiteSpace(CarId)) {
                ErrorIf(CarsManager.Instance.GetWrapperById(CarId) == null,
                        AcErrorType.Replay_CarIsMissing, CarId);
            }

            try {
                DriverName = reader.ReadString();
                reader.ReadInt64();
                CarSkinId = reader.ReadString();
            } catch (Exception) {
                // ignored
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