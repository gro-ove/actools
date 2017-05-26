using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public interface IUserChampionshipInformation {
        string Name { get; }

        string PreviewImage { get; }

        string Description { get; }

        string Author { get; }

        string Difficulty { get; }
    }

    public class UserChampionshipObject : AcCommonSingleFileObject, IAcObjectAuthorInformation, IUserChampionshipInformation, IComparer {
        public const string FileExtension = ".champ";
        public const string FileDataExtension = ".champ.json";
        public const string FilePreviewExtension = ".champ.jpg";

        public static readonly string[] ExtraExtensions = { FileDataExtension, FilePreviewExtension };

        public override string Extension => FileExtension;

        // ReSharper disable once NotNullMemberIsNotInitialized
        // InitializeLocations() will be called immediately
        public UserChampionshipObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        protected override void InitializeLocations() {
            base.InitializeLocations();
            ExtendedFilename = Location.ApartFromLast(FileExtension) + FileDataExtension;
            PreviewImage = Location.ApartFromLast(FileExtension) + FilePreviewExtension;
        }

        protected override void LoadOrThrow() {
            // Base version would load object’s name from it’s filename, we don’t need this
            LoadJsonOrThrow();
        }

        public override void PastLoad() {
            base.PastLoad();
            ReloadExtendedData();
        }

        [NotNull]
        public string ExtendedFilename { get; private set; }

        [NotNull]
        public string PreviewImage { get; private set; }

        public override bool HasData => true;

        public override bool Changed {
            get => base.Changed;
            protected set {
                base.Changed = value;
                if (value) {
                    ChangedData = true;
                } else {
                    ChangedData = false;
                    ChangedExtended = false;
                }
            }
        }

        private void UpdateChanged() {
            base.Changed = ChangedData || ChangedExtended;
        }

        private bool _changedData;

        public bool ChangedData {
            get => _changedData;
            private set {
                if (Equals(value, _changedData)) return;
                _changedData = value;
                OnPropertyChanged();
                UpdateChanged();
            }
        }

        private bool _changedExtended;

        public bool ChangedExtended {
            get => _changedExtended;
            private set {
                if (Equals(value, _changedExtended)) return;
                _changedExtended = value;
                OnPropertyChanged();
                UpdateChanged();
            }
        }

        #region Original properties
        private UserChampionshipRules _rules;

        public UserChampionshipRules Rules {
            get => _rules;
            private set {
                if (Equals(value, _rules)) return;
                _rules = value;
                OnPropertyChanged();
            }
        }

        private List<UserChampionshipDriver> _drivers;

        public IReadOnlyList<UserChampionshipDriver> Drivers {
            get => _drivers;
            set {
                if (Equals(value, _drivers) || _drivers != null && value?.SequenceEqual(_drivers) == true) {
                    return;
                }

                if (value != null) {
                    var player = value.FirstOrDefault(x => x.IsPlayer);
                    if (player == null) {
                        player = UserChampionshipDriver.Default;
                        value = value.Prepend(player).ToList();
                    }
                }

                _drivers = value?.ToListIfItIsNot();

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedData = true;
                }

                if (value != null) {
                    var player = value.First(x => x.IsPlayer);
                    PlayerCarId = player.CarId;
                    PlayerCarSkinId = player.SkinId;

                    var aiLevelTo = 0d;
                    var aiLevelFrom = 100d;

                    foreach (var driver in value) {
                        if (driver.CarId != null && CarsManager.Instance.GetWrapperById(driver.CarId) == null) {
                            AddError(AcErrorType.Data_KunosCareerCarIsMissing, driver.CarId);
                        }

                        if (!driver.IsPlayer) {
                            var aiLevel = driver.AiLevel;
                            if (aiLevel > aiLevelTo) aiLevelTo = aiLevel;
                            if (aiLevel < aiLevelFrom) aiLevelFrom = aiLevel;
                        }
                    }

                    if (aiLevelTo < aiLevelFrom) aiLevelTo = aiLevelFrom;
                    AiLevelTo = aiLevelTo;
                    AiLevelFrom = aiLevelFrom;
                    AiLevelRange = aiLevelTo != aiLevelFrom;
                }

                _championshipDriversView = null;
                OnPropertyChanged(nameof(ChampionshipDriversView));
            }
        }

        #region For scaling AI level
        private double _aiLevelTo;

        public double AiLevelTo {
            get => _aiLevelTo;
            private set {
                if (Equals(value, _aiLevelTo)) return;
                _aiLevelTo = value;

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AiLevelDisplay));
                    OnPropertyChanged(nameof(UserAiLevelTo));
                    OnPropertyChanged(nameof(UserAiLevelDisplay));
                }
            }
        }

        private double _aiLevelFrom;

        public double AiLevelFrom {
            get => _aiLevelFrom;
            private set {
                if (Equals(value, _aiLevelFrom)) return;
                _aiLevelFrom = value;

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AiLevelDisplay));
                    OnPropertyChanged(nameof(UserAiLevelFrom));
                    OnPropertyChanged(nameof(UserAiLevelDisplay));
                }
            }
        }

        private bool _aiLevelRange;

        public bool AiLevelRange {
            get => _aiLevelRange;
            private set {
                if (Equals(value, _aiLevelRange)) return;
                _aiLevelRange = value;

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AiLevelDisplay));
                    OnPropertyChanged(nameof(UserAiLevelDisplay));
                }
            }
        }

        public string AiLevelDisplay => AiLevelRange ? $@"{AiLevelFrom}%–{AiLevelTo}%" : $@"{AiLevelTo}%";

        private class TemporaryConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value?.ToString().ApartFromLast(@"%");
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        // Only to avoid changing ControlsStrings.Common_RecommendedPercentageFormat to something else somehow.
        // I’m not good with locales.
        public static IValueConverter AiLevelDisplayTemporaryConverter { get; } = new TemporaryConverter();
        #endregion

        private UserChampionshipRound[] _rounds;

        public UserChampionshipRound[] Rounds {
            get => _rounds;
            set {
                if (Equals(value, _rounds) || _rounds != null && value?.SequenceEqual(_rounds) == true) {
                    return;
                }

                _rounds = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedData = true;
                }

                if (value != null) {
                    foreach (var round in value) {
                        if (round.TrackId != null && TracksManager.Instance.GetWrapperByKunosId(round.TrackId) == null) {
                            AddError(AcErrorType.Data_KunosCareerTrackIsMissing, round.TrackId);
                        }
                    }
                }
            }
        }

        private int _maxCars;

        public int MaxCars {
            get => _maxCars;
            set {
                if (Equals(value, _maxCars)) return;
                _maxCars = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedData = true;
                }
            }
        }
        #endregion

        #region Car and car’s skin
        private string _playerCarId;

        public string PlayerCarId {
            get => _playerCarId;
            private set {
                if (Equals(value, _playerCarId)) return;

                _playerCarId = value;
                _playerCar = null;
                _playerCarSet = false;

                _playerCarSkin = null;
                _playerCarSkinSet = false;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedData = true;
                    OnPropertyChanged(nameof(PlayerCar));

                    UpdateCoherentTime();
                }
            }
        }

        private CarObject _playerCar;
        private bool _playerCarSet;

        public CarObject PlayerCar {
            get {
                if (!_playerCarSet) {
                    _playerCarSet = true;
                    _playerCar = _playerCarId == null ? null : CarsManager.Instance.GetById(_playerCarId);
                }
                return _playerCar;
            }
            private set => PlayerCarId = value?.Id;
        }

        private string _playerCarSkinId;

        public string PlayerCarSkinId {
            get => _playerCarSkinId;
            private set {
                if (Equals(value, _playerCarSkinId)) return;
                _playerCarSkinId = value;
                _playerCarSkin = null;
                _playerCarSkinSet = false;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedData = true;
                    OnPropertyChanged(nameof(PlayerCarSkin));
                    OnPropertyChanged(nameof(UserPlayerCarSkin));
                }
            }
        }

        private CarSkinObject _playerCarSkin;
        private bool _playerCarSkinSet;

        public CarSkinObject PlayerCarSkin {
            get {
                if (!_playerCarSkinSet) {
                    _playerCarSkinSet = true;
                    _playerCarSkin = _playerCarSkinId == null ? null : PlayerCar.GetSkinById(_playerCarSkinId);
                }
                return _playerCarSkin;
            }
            set => PlayerCarSkinId = value?.Id;
        }

        public void SetPlayerCar(CarObject car, CarSkinObject carSkin = null) {
            PlayerCar = car;
            PlayerCarSkin = carSkin;
        }
        #endregion

        #region Custom properties
        private int _pointsForBestLap;

        public int PointsForBestLap {
            get => _pointsForBestLap;
            set {
                if (Equals(value, _pointsForBestLap)) return;
                _pointsForBestLap = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        private int _pointsForPolePosition;

        public int PointsForPolePosition {
            get => _pointsForPolePosition;
            set {
                if (Equals(value, _pointsForPolePosition)) return;
                _pointsForPolePosition = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        private bool _realConditions;

        public bool RealConditions {
            get => _realConditions;
            set {
                if (Equals(value, _realConditions)) return;
                _realConditions = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        private bool _realConditionsManualTime;

        public bool RealConditionsManualTime {
            get => _realConditionsManualTime;
            set {
                if (Equals(value, _realConditionsManualTime)) return;
                _realConditionsManualTime = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        private bool _coherentTime;

        public bool CoherentTime {
            get => _coherentTime;
            set {
                if (Equals(value, _coherentTime)) return;
                _coherentTime = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                    UpdateCoherentTime();
                }
            }
        }

        private void UpdateMaxCars() {
            MaxCars = (_extendedRounds?.Select(x => x.Track) ??
                    _rounds.Where(x => x.TrackId != null).Select(x => TracksManager.Instance.GetLayoutByKunosId(x.TrackId)))
                    .NonNull()
                    .Select(x => x.SpecsPitboxesValue).MinOr(0);
        }

        private bool _updatingCoherentTime;

        private void UpdateCoherentTime([NotNull] IList<UserChampionshipRoundExtended> rounds) {
            if (!_coherentTime || _updatingCoherentTime || rounds.Count == 0) return;

            try {
                _updatingCoherentTime = true;

                var time = rounds[0].Time;
                var playerCar = PlayerCar;
                var interval = 20 * 60;

                for (var i = 0; i < rounds.Count; i++) {
                    var round = rounds[i];
                    round.Time = time;

                    if (Rules.Practice > 0) {
                        time += Rules.Practice * 60 + interval;
                    }

                    if (Rules.Qualifying > 0) {
                        time += Rules.Qualifying * 60 + interval;
                    }

                    var approximateLapDuration = (int)(round.Track?.GuessApproximateLapDuration(playerCar).TotalSeconds ?? 150d);
                    time = (time + approximateLapDuration * round.LapsCount + interval).Ceiling(15 * 60);

                    if (time > CommonAcConsts.TimeMaximum - 60 * 60) {
                        time = CommonAcConsts.TimeMinimum;
                    }
                }
            } finally {
                _updatingCoherentTime = false;
            }
        }

        private void UpdateCoherentTime() {
            if (_extendedRounds == null) return;
            UpdateCoherentTime(_extendedRounds);
        }

        [CanBeNull]
        private ChangeableObservableCollection<UserChampionshipRoundExtended> _extendedRounds;

        private JArray _extendedRoundsJArray;

        [NotNull]
        public ChangeableObservableCollection<UserChampionshipRoundExtended> ExtendedRounds => _extendedRounds ??
                (_extendedRounds = SetExtendedRounds(_extendedRoundsJArray?.ToObject<UserChampionshipRoundExtended[]>() ?? GetRounds(Rounds)));

        private ChangeableObservableCollection<UserChampionshipRoundExtended> SetExtendedRounds(IEnumerable<UserChampionshipRoundExtended> rounds) {
            var result = new ChangeableObservableCollection<UserChampionshipRoundExtended>(rounds);
            result.CollectionChanged += OnExtendedRoundsCollectionChanged;
            result.ItemPropertyChanged += OnExtendedRoundsItemPropertyChanged;
            UpdateExtendedRoundsIndex(result);
            UpdateCoherentTime(result);
            return result;
        }

        private void OnExtendedRoundsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            UpdateExtendedRoundsIndex();
            UpdateCoherentTime();
            UpdateMaxCars();
            ChangedData = true;
            ChangedExtended = true;
        }

        private void OnExtendedRoundsItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(UserChampionshipRoundExtended.Deleted):
                    var round = (UserChampionshipRoundExtended)sender;
                    if (round.Deleted) {
                        ExtendedRounds.Remove(round);
                    }
                    break;
                case nameof(UserChampionshipRoundExtended.Time):
                    ChangedExtended = true;
                    if (((UserChampionshipRoundExtended)sender).Index == 0) {
                        UpdateCoherentTime();
                    }
                    break;
                case nameof(UserChampionshipRoundExtended.Track):
                    ChangedData = true;
                    ChangedExtended = true;
                    UpdateMaxCars();
                    break;
                case nameof(UserChampionshipRoundExtended.LapsCount):
                    ChangedData = true;
                    ChangedExtended = true;
                    UpdateCoherentTime();
                    break;
                case nameof(UserChampionshipRoundExtended.TrackProperties):
                case nameof(UserChampionshipRoundExtended.Weather):
                    ChangedData = true;
                    ChangedExtended = true;
                    break;
                case nameof(UserChampionshipRoundExtended.Temperature):
                case nameof(UserChampionshipRoundExtended.Description):
                    ChangedExtended = true;
                    break;
            }
        }

        private static void UpdateExtendedRoundsIndex([NotNull] IList<UserChampionshipRoundExtended> rounds) {
            for (var i = 0; i < rounds.Count; i++) {
                rounds[i].Index = i;
            }
        }

        private void UpdateExtendedRoundsIndex() {
            if (_extendedRounds == null) return;
            UpdateExtendedRoundsIndex(_extendedRounds);
        }

        private string _serializedRaceGridData;

        public string SerializedRaceGridData {
            get => _serializedRaceGridData;
            set {
                if (Equals(value, _serializedRaceGridData)) return;
                _serializedRaceGridData = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        private string _difficulty;

        public string Difficulty {
            get => _difficulty;
            set {
                if (Equals(value, _difficulty)) return;
                _difficulty = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        public override string Name {
            get => base.Name;
            protected set {
                if (Equals(value, base.Name)) return;
                base.Name = value;

                if (_code == null) {
                    OnPropertyChanged(nameof(DisplayCode));
                }
            }
        }

        public string DisplayCode => Code ?? (DisplayName.Length > 0 ? DisplayName?.Substring(0, 1) : @"?");

        private string _code;

        public string Code {
            get => _code;
            set {
                if (value != null) {
                    value = value.Trim();
                    if (value.Length == 0) value = null;
                }

                if (Equals(value, _code)) return;
                _code = value;

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayCode));
                    ChangedExtended = true;
                }
            }
        }

        private string _description;

        public string Description {
            get => _description;
            set {
                if (Equals(value, _description)) return;
                _description = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        private Rect? _previewCrop;

        /// <summary>
        /// In absolute units.
        /// </summary>
        public Rect? PreviewCrop {
            get => _previewCrop;
            set {
                if (Equals(value, _previewCrop)) return;
                _previewCrop = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }

        public bool ChampionshipPointsGoalType {
            get => _championshipPointsGoal > 0;
            set {
                if (Equals(value, ChampionshipPointsGoalType)) return;
                if (ChampionshipPointsGoal == 0) {
                    var perRound = Rules.Points.ElementAtOrDefault(1);
                    if (perRound == 0) {
                        perRound = Rules.Points.FirstOrDefault();
                    }

                    ChampionshipPointsGoal = _rounds.Length * perRound;
                } else {
                    ChampionshipPointsGoal = -ChampionshipPointsGoal;
                }
            }
        }

        private int _championshipPointsGoal;

        public int ChampionshipPointsGoal {
            get => _championshipPointsGoal;
            set {
                if (Equals(value, _championshipPointsGoal)) return;

                var previousValue = _championshipPointsGoal;
                _championshipPointsGoal = value;

                if (Loaded) {
                    OnPropertyChanged();
                    if (previousValue > 0 ^ value > 0) {
                        OnPropertyChanged(nameof(ChampionshipPointsGoalType));
                    }

                    ChangedExtended = true;
                }
            }
        }

        private int _championshipRankingGoal = 1;

        public int ChampionshipRankingGoal {
            get => _championshipRankingGoal;
            set {
                value = value.Clamp(1, 99999);
                if (Equals(value, _championshipRankingGoal)) return;
                _championshipRankingGoal = value;

                if (Loaded) {
                    OnPropertyChanged();
                    ChangedExtended = true;
                }
            }
        }
        #endregion

        public void ReloadJsonData() {
            ClearErrors(AcErrorCategory.Data);
            LoadJsonOrThrow();
            ChangedData = false;
            LoadProgress();
        }

        private IEnumerable<UserChampionshipRoundExtended> GetRounds([NotNull] IEnumerable<UserChampionshipRound> rounds) {
            return rounds.Select(x => {
                if (x.TrackId == null) return null;

                var track = TracksManager.Instance.GetLayoutByKunosId(x.TrackId);
                if (track == null) {
                    AddError(AcErrorType.Data_UserChampionshipTrackIsMissing, x.TrackId);
                    return null;
                }

                var weather = (WeatherObject)WeatherManager.Instance.WrappersList.Where(y => y.Value.Enabled).ElementAtOrDefault(x.Weather)?.Loaded() ??
                        WeatherManager.Instance.GetDefault();
                var trackProperties = Game.DefaultTrackPropertiesPresets.ElementAtOrDefault(x.Surface) ?? Game.GetDefaultTrackPropertiesPreset();
                return new UserChampionshipRoundExtended (track) {
                    LapsCount = x.LapsCount,
                    TrackProperties = trackProperties,
                    Weather = weather
                };
            }).NonNull();
        }

        private JObject _jsonObject;

        private void LoadJsonOrThrow() {
            try {
                _jsonObject = JsonExtension.Parse(File.ReadAllText(Location));

                Name = _jsonObject.GetStringValueOnly("name");
                MaxCars = _jsonObject.GetIntValueOnly("maxCars") ?? -1;

                Rules = _jsonObject[@"rules"]?.ToObject<UserChampionshipRules>() ?? new UserChampionshipRules();
                Rules.PropertyChanged += OnRulesPropertyChanged;

                Drivers = (IReadOnlyList<UserChampionshipDriver>)_jsonObject[@"opponents"]?.ToObject<List<UserChampionshipDriver>>() ??
                        new[] { UserChampionshipDriver.Default };
                Rounds = _jsonObject[@"rounds"]?.ToObject<UserChampionshipRound[]>() ?? new[] { UserChampionshipRound.Default };
            } catch (Exception e) {
                Logging.Warning(e);
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(Location));
                ClearData();
            }
        }

        private void SaveJson() {
            var json = _jsonObject ?? new JObject();

            // Prepare stuff
            UpdateMaxCars();

            // Save base params
            json[@"name"] = Name;
            json[@"changedByCm"] = true;
            json[@"maxCars"] = MaxCars;

            // Save rules
            var jobj = json[@"rules"] as JObject;
            if (jobj == null) {
                json[@"rules"] = jobj = new JObject();
            }
            Rules.SaveTo(jobj);

            // Save rounds
            if (_extendedRounds != null) {
                json[@"rounds"] = JArray.FromObject(_extendedRounds.Select(x =>
                        new UserChampionshipRound(x.Track?.KunosIdWithLayout ?? x.TrackId.Replace('/', '-'),
                                x.LapsCount,
                                WeatherManager.Instance.WrappersList.FindIndex(y => ReferenceEquals(y.Value, x.Weather)),
                                Game.DefaultTrackPropertiesPresets.IndexOf(x.TrackProperties))));
            } else {
                json[@"rounds"] = _rounds == null ? null : JArray.FromObject(_rounds);
            }

            // Save opponents
            // Important note: while extended rounds are here, we can automatically convert them
            // to Kunos ones while saving, but it won’t work with extended racing grid. So, while changing it
            // outside, don’t forget to update Drivers array as well.
            var player = new UserChampionshipDriver(UserChampionshipDriver.PlayerName, PlayerCarId, PlayerCarSkinId?.ToLowerInvariant());
            json[@"opponents"] = JArray.FromObject(Drivers.Where(x => !x.IsPlayer).Prepend(player));

            // Writing to a file
            File.WriteAllText(Location, json.ToString(Formatting.Indented));
            RemoveError(AcErrorType.Data_JsonIsDamaged);
        }

        private void ClearData() {
            Name = Id.ApartFromLast(Extension);
            MaxCars = -1;
            Rules = new UserChampionshipRules();
            Drivers = new UserChampionshipDriver[0];
            Rounds = new UserChampionshipRound[0];
        }

        private void OnRulesPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(UserChampionshipRules.Practice):
                case nameof(UserChampionshipRules.Qualifying):
                    UpdateCoherentTime();
                    break;
            }

            ChangedData = true;
        }

        private JObject _extraJsonObject;

        public void ReloadExtendedData() {
            ClearErrors(AcErrorCategory.ExtendedData);
            LoadExtended();
            LoadProgress();
            ChangedExtended = false;
        }

        private void LoadExtended() {
            try {
                if (!File.Exists(ExtendedFilename)) {
                    ClearExtendedData();
                    return;
                }

                var obsolete = new FileInfo(ExtendedFilename).LastWriteTime < new FileInfo(Location).LastWriteTime &&
                        _jsonObject.GetBoolValueOnly("changedByCm") != true;

                var json = JsonExtension.Parse(File.ReadAllText(ExtendedFilename));
                _extraJsonObject = json;

                Code = json.GetStringValueOnly("code");
                ChampionshipPointsGoal = json.GetIntValueOnly("championshipPointsGoal") ?? 0;
                ChampionshipRankingGoal = json.GetIntValueOnly("championshipRankingGoal") ?? 1;
                PointsForBestLap = json.GetIntValueOnly("pointsForBestLap") ?? 0;
                PointsForPolePosition = json.GetIntValueOnly("pointsForPolePosition") ?? 0;
                Description = json.GetStringValueOnly("description");
                Difficulty = json.GetStringValueOnly("difficulty");
                CoherentTime = json.GetBoolValueOnly("coherentTime") ?? false;
                RealConditions = json.GetBoolValueOnly("realConditions") ?? false;
                RealConditionsManualTime = json.GetBoolValueOnly("realConditionsManualTime") ?? false;

                Author = json.GetStringValueOnly("author")?.Trim();
                Version = json.GetStringValueOnly("version")?.Trim();
                Url = json.GetStringValueOnly("url")?.Trim();

                var crop = json.GetStringValueOnly("previewCrop");
                PreviewCrop = crop == null ? (Rect?)null : Rect.Parse(crop);
                SerializedRaceGridData = json.GetStringValueOnly("raceGridData");

                if (obsolete) {
                    _extendedRoundsJArray = null;
                    _extendedRounds = null;
                    OnPropertyChanged(nameof(ExtendedRounds));
                } else {
                    // We don’t want to automatically initialize Extended Rounds, because it would require
                    // a bunch of tracks to be loaded, might be slow.
                    _extendedRoundsJArray = json[@"rounds"] as JArray;
                    _extendedRounds = null;
                    OnPropertyChanged(nameof(ExtendedRounds));
                }
            } catch (Exception e) {
                Logging.Warning(e);
                AddError(AcErrorType.ExtendedData_JsonIsDamaged, Path.GetFileName(Location));
                ClearExtendedData();
            }
        }

        private void SaveExtended() {
            var json = _extraJsonObject ?? new JObject();

            // Prepare stuff
            UpdateCoherentTime();

            json.SetNonDefault("code", Code);
            json.SetNonDefault("championshipPointsGoal", ChampionshipPointsGoal);
            json.SetNonDefault("championshipRankingGoal", ChampionshipRankingGoal);
            json.SetNonDefault("pointsForBestLap", PointsForBestLap);
            json.SetNonDefault("pointsForPolePosition", PointsForPolePosition);
            json.SetNonDefault("code", Code);
            json.SetNonDefault("description", Description);
            json.SetNonDefault("difficulty", Difficulty);
            json.SetNonDefault("coherentTime", CoherentTime);
            json.SetNonDefault("realConditions", RealConditions);
            json.SetNonDefault("realConditionsManualTime", RealConditionsManualTime);
            json.SetNonDefault("author", Author);
            json.SetNonDefault("version", Version);
            json.SetNonDefault("url", Url);

            json[@"previewCrop"] = PreviewCrop?.ToString(CultureInfo.InvariantCulture);
            json[@"rounds"] = _extendedRounds != null ? JArray.FromObject(_extendedRounds) : _extendedRoundsJArray;
            json[@"raceGridData"] = SerializedRaceGridData;

            // Writing to a file
            File.WriteAllText(ExtendedFilename, json.ToString(Formatting.Indented));
            RemoveError(AcErrorType.ExtendedData_JsonIsDamaged);
        }

        private void ClearExtendedData() {
            Code = null;
            ChampionshipPointsGoal = 0;
            ChampionshipRankingGoal = 1;
            Description = null;
            Difficulty = null;
            Author = null;
            Version = null;
            Url = null;
            SerializedRaceGridData = null;
            _extendedRoundsJArray = null;
            _extendedRounds = null;
            OnPropertyChanged(nameof(ExtendedRounds));
        }

        private DateTime _lastSavedData, _lastSavedExtended;

        public override void Save() {
            // Base version would rename file if name changed, we don’t need this

            if (ChangedData) {
                try {
                    SaveJson();
                    ChangedData = false;
                    _lastSavedData = DateTime.Now;
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save data", e);
                }
            }

            if (ChangedExtended) {
                try {
                    SaveExtended();
                    ChangedExtended = false;
                    _lastSavedExtended = DateTime.Now;
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save extra data", e);
                }
            }
        }

        public override bool HandleChangedFile(string filename) {
            if (FileUtils.IsAffected(filename, PreviewImage)) {
                // OnImageChanged(nameof(PreviewImage));
                OnImageChangedValue(PreviewImage);
                return true;
            }

            if (string.Equals(filename, ExtendedFilename, StringComparison.OrdinalIgnoreCase) && (DateTime.Now - _lastSavedExtended).TotalSeconds > 5d) {
                ReloadExtendedData();
                return true;
            }

            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase) && (DateTime.Now - _lastSavedData).TotalSeconds > 5d) {
                ReloadJsonData();
            }

            return true;
        }

        #region Version info
        private string _author;

        [CanBeNull]
        public string Author {
            get => _author;
            set {
                if (value == _author) return;
                _author = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

                if (Loaded) {
                    OnPropertyChanged(nameof(Author));
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    ChangedExtended = true;
                    SuggestionLists.RebuildAuthorsList();
                }
            }
        }

        private string _version;

        [CanBeNull]
        public string Version {
            get => _version;
            set {
                if (value == _version) return;
                _version = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

                if (Loaded) {
                    OnPropertyChanged(nameof(Version));
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    ChangedExtended = true;
                }
            }
        }

        private string _url;

        [CanBeNull]
        public string Url {
            get => _url;
            set {
                if (value == _url) return;
                _url = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

                if (Loaded) {
                    OnPropertyChanged(nameof(Url));
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    ChangedExtended = true;
                }
            }
        }

        public string VersionInfoDisplay => this.GetVersionInfoDisplay();
        #endregion

        #region Mimicking KunosCareerObject (might be useful later)
        public bool IsAvailable { get; } = true;

        public string DisplayRequired { get; } = null;

        public KunosCareerObjectType Type { get; } = KunosCareerObjectType.Championship;

        public int FirstPlacesGoal { get; } = 0;

        public int SecondPlacesGoal { get; } = 0;

        public int ThirdPlacesGoal { get; } = 0;
        #endregion

        #region Custom AI level
        private double _userAiLevelMultipler = 1;

        public double UserAiLevelMultipler {
            get => _userAiLevelMultipler;
            set {
                value = value.Clamp(0.5, 1.5);
                if (Equals(value, _userAiLevelMultipler)) return;
                _userAiLevelMultipler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UserAiLevelFrom));
                OnPropertyChanged(nameof(UserAiLevelTo));
                OnPropertyChanged(nameof(UserAiLevelDisplay));
            }
        }

        private DelegateCommand _resetUserAiLevelCommand;

        public DelegateCommand ResetUserAiLevelCommand => _resetUserAiLevelCommand ?? (_resetUserAiLevelCommand = new DelegateCommand(() => {
            UserAiLevelMultipler = 1d;
        }));

        public double UserAiLevelFrom => Math.Min(AiLevelFrom * UserAiLevelMultipler, 100d);

        public double UserAiLevelTo => Math.Min(AiLevelTo * UserAiLevelMultipler, 100d);

        public string UserAiLevelDisplay => AiLevelRange ? $@"{UserAiLevelFrom:F0}%–{UserAiLevelTo:F0}%" : $@"{UserAiLevelTo:F0}%";
        #endregion

        #region Progress
        public void ResetSkinToDefault() {
            UserPlayerCarSkin = null;
        }

        private CarSkinObject _userPlayerCarSkin;

        public CarSkinObject UserPlayerCarSkin {
            get => _userPlayerCarSkin ?? PlayerCarSkin;
            set {
                if (Equals(value, _userPlayerCarSkin)) return;
                _userPlayerCarSkin = value;
                OnPropertyChanged();
            }
        }

        private int _firstPlaces;

        public int FirstPlaces {
            get => _firstPlaces;
            set {
                if (value == _firstPlaces) return;
                _firstPlaces = value;
                OnPropertyChanged();
            }
        }

        private int _secondPlaces;

        public int SecondPlaces {
            get => _secondPlaces;
            set {
                if (value == _secondPlaces) return;
                _secondPlaces = value;
                OnPropertyChanged();
            }
        }

        private int _thirdPlaces;

        public int ThirdPlaces {
            get => _thirdPlaces;
            set {
                if (value == _thirdPlaces) return;
                _thirdPlaces = value;
                OnPropertyChanged();
            }
        }

        private long _lastSelectedTimestamp;

        public long LastSelectedTimestamp {
            get => _lastSelectedTimestamp;
            set {
                if (Equals(value, _lastSelectedTimestamp)) return;
                _lastSelectedTimestamp = value;

                if (Loaded) {
                    OnPropertyChanged();
                    SaveProgress(false);
                }
            }
        }

        public string DisplayChampionshipGoal
            => ChampionshipPointsGoalType ? $@"[b]{ChampionshipPointsGoal}[/b] {PluralizingConverter.Pluralize(ChampionshipPointsGoal, ToolsStrings.KunosCareer_Point)}"
                    : $@"[b]{ChampionshipRankingGoal.ToOrdinalShort(ToolsStrings.KunosCareer_Place)}[/b] {ToolsStrings.KunosCareer_Place}";

        public string DisplayType => Type.GetDescription();

        public bool IsStarted => CompletedEvents > 0;

        public string DisplayGo => CompletedEvents > 0 ? ToolsStrings.KunosCareer_Resume : ToolsStrings.KunosCareer_Start;

        private int _completedEvents;

        public int CompletedEvents {
            get => _completedEvents;
            set {
                if (Equals(value, _completedEvents)) return;
                _completedEvents = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayGo));
                OnPropertyChanged(nameof(IsStarted));
                _championshipResetCommand?.RaiseCanExecuteChanged();
            }
        }

        private int _championshipPointsNeeded;

        public int ChampionshipPointsNeeded {
            get => _championshipPointsNeeded;
            set {
                if (Equals(value, _championshipPointsNeeded)) return;
                _championshipPointsNeeded = value;
                OnPropertyChanged();
            }
        }

        private bool _isCompleted;

        public bool IsCompleted {
            get => _isCompleted;
            set {
                if (Equals(value, _isCompleted)) return;
                _isCompleted = value;
                OnPropertyChanged();
            }
        }

        private bool _isFinished;

        public bool IsFinished {
            get => _isFinished;
            set {
                if (Equals(value, _isFinished)) return;
                _isFinished = value;
                OnPropertyChanged();
            }
        }

        private int _championshipPoints = -1;

        public int ChampionshipPoints {
            get => _championshipPoints;
            set {
                if (Equals(value, _championshipPoints)) return;
                _championshipPoints = value;
                OnPropertyChanged();
                var player = Drivers.FirstOrDefault(x => x.IsPlayer);
                if (player != null) {
                    Drivers.First(x => x.IsPlayer).Points = value;
                } else {
                    Logging.Unexpected();
                    Logging.Warning(Drivers.Select(x => x.Name).JoinToString(@", "));
                }
            }
        }

        private int _championshipPlace;

        public int ChampionshipPlace {
            get => _championshipPlace;
            private set {
                if (Equals(value, _championshipPlace)) return;
                _championshipPlace = value;
                OnPropertyChanged();
            }
        }

        private UserChampionshipRoundExtended _currentRound;

        public UserChampionshipRoundExtended CurrentRound {
            get => _currentRound;
            set {
                if (Equals(value, _currentRound)) return;
                _currentRound = value;
                OnPropertyChanged();
            }
        }

        public int Compare(object x, object y) {
            var xc = x as UserChampionshipDriver;
            var yc = y as UserChampionshipDriver;
            if (xc == null || yc == null) return 0;
            return yc.Points - xc.Points;
        }

        private ListCollectionView _championshipDriversView;

        public ListCollectionView ChampionshipDriversView {
            get => _championshipDriversView ?? (_championshipDriversView = new ListCollectionView(_drivers) { CustomSort = this });
            set {
                if (Equals(value, _championshipDriversView)) return;
                _championshipDriversView = value;
                OnPropertyChanged();
            }
        }

        public void LoadProgress() {
            LoadProgressFromEntry();
            RecalculateProgressValues();
        }

        public void UpdateTakenPlaces() {
            var place = 0;
            var prevPoints = -1;
            var prevPlace = -1;
            foreach (var driver in Drivers.OrderByDescending(x => x.Points)) {
                ++place;

                if (driver.Points <= 0) {
                    driver.TakenPlace = 0;
                } else {
                    if (driver.Points == prevPoints) {
                        driver.TakenPlace = prevPlace;
                    } else {
                        driver.TakenPlace = place;
                        prevPoints = driver.Points;
                        prevPlace = place;
                    }
                }

                if (driver.IsPlayer) {
                    ChampionshipPlace = driver.TakenPlace == 0 ? Drivers.Count : driver.TakenPlace;
                }
            }
        }

        private void LoadProgressFromEntry() {
            UpdateExtendedRoundsIndex();

            var entryId = Id.ApartFromLast(FileExtension).ToLowerInvariant();
            var entry = UserChampionshipsProgress.Instance.Entries.GetValueOrDefault(entryId);
            if (entry == null) {
                CompletedEvents = 0;
                IsFinished = false;
                IsCompleted = false;

                foreach (var driver in Drivers) {
                    driver.Points = 0;
                }

                ChampionshipPoints = 0;
                UpdateTakenPlaces();

                _lastSelectedTimestamp = 0;
                CurrentRound = ExtendedRounds.First();

                foreach (var round in ExtendedRounds) {
                    round.TakenPlace = Type == KunosCareerObjectType.SingleEvents ? PlaceConditions.UnremarkablePlace : 0;
                    round.IsAvailable = Type == KunosCareerObjectType.SingleEvents || round.Index == 0;
                    round.IsPassed = false;
                    return;
                }
            } else {
                var count = ExtendedRounds.Count;
                CompletedEvents = entry.EventsResults.Where(x => x.Key < count).Count(x => x.Value > 0);
                IsFinished = CompletedEvents == ExtendedRounds.Count;

                if (Type == KunosCareerObjectType.SingleEvents) {
                    FirstPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 3);
                    SecondPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 2);
                    ThirdPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 1);
                    ChampionshipPoints = 0;

                    foreach (var driver in Drivers) {
                        driver.Points = 0;
                    }

                    UpdateTakenPlaces();
                } else {
                    FirstPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 1);
                    SecondPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 2);
                    ThirdPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 3);
                    ChampionshipPoints = entry.Points ?? 0;

                    for (var i = Drivers.Count - 1; i >= 0; i--) {
                        var driver = Drivers[i];
                        driver.Points = driver.IsPlayer ? ChampionshipPoints : entry.AiPoints.GetValueOrDefault(i - 1);
                    }

                    UpdateTakenPlaces();
                }

                _lastSelectedTimestamp = 1;
                CurrentRound = ExtendedRounds.ElementAtOrDefault(entry.SelectedEvent) ?? ExtendedRounds.FirstOrDefault();
                IsCompleted = IsFinished && (ChampionshipPointsGoalType ? ChampionshipPoints >= ChampionshipPointsGoal :
                        ChampionshipPlace > 0 && ChampionshipPlace <= ChampionshipRankingGoal);

                foreach (var round in ExtendedRounds) {
                    var takenPlace = entry.EventsResults.GetValueOrDefault(round.Index);
                    if (Type == KunosCareerObjectType.SingleEvents) {
                        if (takenPlace > 0 && takenPlace < 4) {
                            takenPlace = 4 - takenPlace;
                        } else {
                            takenPlace = PlaceConditions.UnremarkablePlace;
                        }
                    }

                    round.TakenPlace = takenPlace;

                    if (Type == KunosCareerObjectType.SingleEvents) {
                        round.IsAvailable = true;
                        round.IsPassed = false;
                    } else {
                        round.IsAvailable = takenPlace == 0 && entry.SelectedEvent == round.Index;
                        round.IsPassed = takenPlace != 0;
                    }
                }
            }

            _championshipDriversView?.Refresh();
            OnPropertyChanged(nameof(LastSelectedTimestamp));
        }

        private void RecalculateProgressValues() {
            /*var firstPlacesNeeded = FirstPlacesGoal - FirstPlaces;
            var secondPlacesNeeded = SecondPlacesGoal - SecondPlaces;
            var thirdPlacesNeeded = ThirdPlacesGoal - ThirdPlaces;

            if (firstPlacesNeeded < 0) {
                secondPlacesNeeded += firstPlacesNeeded;
                firstPlacesNeeded = 0;
            }

            if (secondPlacesNeeded < 0) {
                thirdPlacesNeeded += secondPlacesNeeded;
                secondPlacesNeeded = 0;
            }

            if (thirdPlacesNeeded < 0) {
                thirdPlacesNeeded = 0;
            }

            FirstPlacesNeeded = firstPlacesNeeded;
            SecondPlacesNeeded = secondPlacesNeeded;
            ThirdPlacesNeeded = thirdPlacesNeeded;*/

            ChampionshipPointsNeeded = ChampionshipPointsGoal - ChampionshipPoints;
        }

        public void SaveProgress(bool globalUpdate) {
            switch (Type) {
                case KunosCareerObjectType.Championship:
                    var events = ExtendedRounds.ToList();
                    UserChampionshipsProgress.Instance.UpdateEntry(Id.ApartFromLast(FileExtension),
                            new UserChampionshipProgressEntry(
                                    events.FirstOrDefault(x => x.IsAvailable && !x.IsPassed)?.Index ?? events.LastOrDefault()?.Index ?? 0,
                                    events.Select((x, i) => new {
                                        Key = i,
                                        Place = x.TakenPlace
                                    }).TakeWhile(x => x.Place != 0).ToDictionary(x => x.Key, x => x.Place),
                                    ChampionshipPoints,
                                    Drivers.Where(x => !x.IsPlayer).Select((x, i) => new {
                                        Key = i,
                                        x.Points
                                    }).ToDictionary(x => x.Key, x => x.Points)),
                            globalUpdate);
                    break;
                case KunosCareerObjectType.SingleEvents:
                    throw new NotSupportedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private DelegateCommand _championshipResetCommand;

        public DelegateCommand ChampionshipResetCommand => _championshipResetCommand ?? (_championshipResetCommand = new DelegateCommand(() => {
            UserChampionshipsProgress.Instance.UpdateEntry(Id.ApartFromLast(FileExtension), new UserChampionshipProgressEntry(0, null, 0, null), true);
        }, () => Type == KunosCareerObjectType.Championship && IsStarted));
        #endregion
    }
}