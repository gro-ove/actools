using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Objects {
    public class KunosCareerEventObject : AcIniObject {
        public string KunosCareerId { get; }

        public KunosCareerObjectType KunosCareerType { get; }

        /// <summary>
        /// Starting from 0, like in career.ini!
        /// </summary>
        public int EventNumber { get; }

        public KunosCareerEventObject(string kunosCareerId, KunosCareerObjectType type, IFileAcManager manager, string fileName, bool enabled)
                : base(manager, fileName, enabled) {
            KunosCareerId = kunosCareerId;
            EventNumber = FlexibleParser.ParseInt(fileName.Substring("event".Length)) - 1;
            KunosCareerType = type;
        }

        public override string IniFilename => Path.Combine(Location, "event.ini");

        public string PreviewImage => ImageRefreshing ?? Path.Combine(Location, "preview.png");

        #region From main ini file
        private string _description;

        public string Description {
            get { return _description; }
            set {
                if (Equals(value, _description)) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        private string _displayType;

        public string DisplayType {
            get { return _displayType; }
            set {
                if (Equals(value, _displayType)) return;
                _displayType = value;
                OnPropertyChanged();
            }
        }

        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                OnPropertyChanged();
            }
        }

        private string _trackConfigurationId;

        public string TrackConfigurationId {
            get { return _trackConfigurationId; }
            set {
                if (Equals(value, _trackConfigurationId)) return;
                _trackConfigurationId = value;
                OnPropertyChanged();
            }
        }

        private string _carId;

        public string CarId {
            get { return _carId; }
            set {
                if (Equals(value, _carId)) return;
                _carId = value;
                OnPropertyChanged();
            }
        }

        private string _carSkinId;

        public string CarSkinId {
            get { return _carSkinId; }
            set {
                if (Equals(value, _carSkinId)) return;
                _carSkinId = value;
                OnPropertyChanged();
            }
        }

        private string _weatherId;

        public string WeatherId {
            get { return _weatherId; }
            set {
                if (Equals(value, _weatherId)) return;
                _weatherId = value;
                OnPropertyChanged();
            }
        }

        private PlaceConditionsType? _conditionType;

        /// <summary>
        /// Champinoship — null, otherwise has value.
        /// </summary>
        public PlaceConditionsType? ConditionType {
            get { return _conditionType; }
            set {
                if (Equals(value, _conditionType)) return;
                _conditionType = value;
                OnPropertyChanged();
                GoCommand.OnCanExecuteChanged();
            }
        }

        private int? _firstPlaceTarget;

        public int? FirstPlaceTarget {
            get { return _firstPlaceTarget; }
            set {
                if (Equals(value, _firstPlaceTarget)) return;
                _firstPlaceTarget = value;
                OnPropertyChanged();
            }
        }

        private int? _secondPlaceTarget;

        public int? SecondPlaceTarget {
            get { return _secondPlaceTarget; }
            set {
                if (Equals(value, _secondPlaceTarget)) return;
                _secondPlaceTarget = value;
                OnPropertyChanged();
            }
        }

        private int? _thirdPlaceTarget;

        public int? ThirdPlaceTarget {
            get { return _thirdPlaceTarget; }
            set {
                if (Equals(value, _thirdPlaceTarget)) return;
                _thirdPlaceTarget = value;
                OnPropertyChanged();
            }
        }

        private int _time;

        public int Time {
            get { return _time; }
            set {
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTime));
            }
        }

        public string DisplayTime {
            get { return $"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}"; }
            set {
                int time;
                if (!FlexibleParser.TryParseTime(value, out time)) return;
                Time = time;
            }
        }

        private int? _startingPosition;

        public int? StartingPosition {
            get { return _startingPosition; }
            set {
                if (Equals(value, _startingPosition)) return;
                _startingPosition = value;
                OnPropertyChanged();
            }
        }

        private int _opponentsCount;

        public int OpponentsCount {
            get { return _opponentsCount; }
            set {
                if (Equals(value, _opponentsCount)) return;
                _opponentsCount = value;
                OnPropertyChanged();
            }
        }

        private int? _laps;

        public int? Laps {
            get { return _laps; }
            set {
                if (Equals(value, _laps)) return;
                _laps = value;
                OnPropertyChanged();
            }
        }

        private int _aiLevel;

        public int AiLevel {
            get { return _aiLevel; }
            set {
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
            }
        }

        private int _userAiLevel;

        private string UserAiLevelKey => $"KunosCareerEventObject.UserAiLevel_{KunosCareerId}__{Id}";

        public int UserAiLevel {
            get { return _userAiLevel; }
            set {
                if (Equals(value, _userAiLevel)) return;
                _userAiLevel = value;
                OnPropertyChanged();
                ResetUserAiLevelCommand.OnCanExecuteChanged();

                if (value == AiLevel) {
                    ValuesStorage.Remove(UserAiLevelKey);
                } else {
                    ValuesStorage.Set(UserAiLevelKey, value);
                }
            }
        }
        #endregion

        #region Loadable objects
        private TrackBaseObject _trackObject;

        public TrackBaseObject TrackObject {
            get { return _trackObject; }
            set {
                if (Equals(value, _trackObject)) return;
                _trackObject = value;
                OnPropertyChanged();
            }
        }

        private CarObject _car;

        public CarObject Car {
            get { return _car; }
            set {
                if (Equals(value, _car)) return;
                _car = value;
                OnPropertyChanged();
            }
        }

        private CarSkinObject _carSkin;

        public CarSkinObject CarSkin {
            get { return _carSkin; }
            set {
                if (Equals(value, _carSkin)) return;
                _carSkin = value;
                OnPropertyChanged();
            }
        }

        private WeatherObject _weatherObject;

        public WeatherObject WeatherObject {
            get { return _weatherObject; }
            set {
                if (Equals(value, _weatherObject)) return;
                _weatherObject = value;
                OnPropertyChanged();
            }
        }

        private double _temperature;

        public double Temperature {
            get { return _temperature; }
            set {
                if (Equals(value, _temperature)) return;
                _temperature = value;
                OnPropertyChanged();
            }
        }

        private double _roadTemperature;

        public double RoadTemperature {
            get { return _roadTemperature; }
            set {
                if (Equals(value, _roadTemperature)) return;
                _roadTemperature = value;
                OnPropertyChanged();
            }
        }

        private Game.TrackPropertiesPreset _trackPreset;

        public Game.TrackPropertiesPreset TrackPreset {
            get { return _trackPreset; }
            set {
                if (Equals(value, _trackPreset)) return;
                _trackPreset = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Progress values
        private int _takenPlace;

        /// <summary>
        /// Starts from 1.
        /// </summary>
        public int TakenPlace {
            get { return _takenPlace; }
            set {
                if (Equals(value, _takenPlace)) return;
                _takenPlace = value;
                OnPropertyChanged();
            }
        }

        private bool _isAvailable;

        public bool IsAvailable {
            get { return _isAvailable; }
            set {
                if (Equals(value, _isAvailable)) return;
                _isAvailable = value;
                OnPropertyChanged();
                GoCommand.OnCanExecuteChanged();
            }
        }

        private bool _isPassed;

        public bool IsPassed {
            get { return _isPassed; }
            set {
                if (Equals(value, _isPassed)) return;
                _isPassed = value;
                OnPropertyChanged();
            }
        }
        #endregion

        protected override void LoadData(IniFile ini) {
            Name = ini["EVENT"].Get("NAME");
            Description = AcStringValues.DecodeDescription(ini["EVENT"].Get("DESCRIPTION"));

            TrackId = ini["RACE"].Get("TRACK");
            TrackConfigurationId = ini["RACE"].Get("CONFIG_TRACK");
            CarId = ini["RACE"].Get("MODEL");
            CarSkinId = ini["CAR_0"].Get("SKIN");
            WeatherId = ini["WEATHER"].Get("NAME") ?? WeatherManager.Instance.GetDefault()?.Id;

            TrackObject = TracksManager.Instance.GetById(TrackId);
            Car = CarsManager.Instance.GetById(CarId);
            WeatherObject = WeatherManager.Instance.GetById(WeatherId ?? string.Empty);

            if (TrackObject == null) {
                AddError(AcErrorType.Data_KunosCareerTrackIsMissing, TrackId);
            }

            if (Car == null) {
                AddError(AcErrorType.Data_KunosCareerCarIsMissing, CarId);
            } else { 
                CarSkin = Car.GetSkinByIdFromConfig(CarSkinId);

                if (CarSkin == null) {
                    AddError(AcErrorType.Data_KunosCareerCarSkinIsMissing, CarId, CarSkinId);
                    CarSkin = Car.GetFirstSkinOrNull();
                }
            }

            if (WeatherObject == null) {
                AddError(AcErrorType.Data_KunosCareerWeatherIsMissing, WeatherId);
            }

            Temperature = ini["TEMPERATURE"].GetDouble("AMBIENT", 26);
            RoadTemperature = ini["TEMPERATURE"].GetDouble("ROAD", 32);

            TrackPreset = Game.DefaultTrackPropertiesPresets.ElementAtOrDefault(ini["DYNAMIC_TRACK"].GetInt("PRESET", 5) - 1) ??
                    Game.DefaultTrackPropertiesPresets[4];

            DisplayType = ini.ContainsKey("SESSION_1") ? "Weekend" : (ini["SESSION_0"].Get("NAME") ?? "Race");

            StartingPosition = ini["SESSION_0"].GetIntNullable("STARTING_POSITION");
            OpponentsCount = ini["RACE"].GetInt("CARS", 1) - 1;

            if (StartingPosition != null || ini.ContainsKey("SESSION_1")) {
                Laps = ini["RACE"].GetInt("RACE_LAPS", 0);
            } else {
                Laps = null;
            }

            AiLevel = ini["RACE"].GetInt("AI_LEVEL", 100);
            UserAiLevel = ValuesStorage.GetInt(UserAiLevelKey, AiLevel);

            if (KunosCareerType == KunosCareerObjectType.SingleEvents) {
                var conditions = LinqExtension.RangeFrom()
                        .Select(x => $"CONDITION_{x}")
                        .TakeWhile(ini.ContainsKey)
                        .Select(x => new {
                            Type = ini[x].GetEnumNullable<PlaceConditionsType>("TYPE"),
                            Value = ini[x].GetIntNullable("OBJECTIVE")
                        })
                        .ToList();

                if (conditions.Count != 3 || conditions[0].Type == null ||
                        conditions.Any(x => x.Value == null || x.Type != null && x.Type != conditions[0].Type)) {
                    Logging.Warning("[KUNOSCAREEREVENTOBJECT] Unsupported conditions: " + conditions.Select(x => $"type: {x.Type}, value: {x.Type}").JoinToString("\n"));
                    AddError(AcErrorType.Data_KunosCareerConditions);
                } else {
                    ConditionType = conditions[0].Type.Value;
                    FirstPlaceTarget = conditions[2].Value.Value;
                    SecondPlaceTarget = conditions[1].Value.Value;
                    ThirdPlaceTarget = conditions[0].Value.Value;
                }
            } else {
                ConditionType = null;
                FirstPlaceTarget = SecondPlaceTarget = ThirdPlaceTarget = null;
            }

            Time = (int)Game.ConditionProperties.GetSeconds(ini["LIGHTING"].GetInt("SUN_ANGLE", 40));
            LoadProgress();
        }

        public void LoadProgress() {
            var entry = KunosCareerProgress.Instance.Entries.GetValueOrDefault(KunosCareerId);
            if (entry == null) {
                TakenPlace = 0;
                IsAvailable = KunosCareerType == KunosCareerObjectType.SingleEvents || EventNumber == 0;
                IsPassed = false;
                return;
            }

            var takenPlace = entry.EventsResults.ElementAtOrDefault(EventNumber);
            if (KunosCareerType == KunosCareerObjectType.SingleEvents) {
                if (takenPlace > 0 && takenPlace < 4) {
                    takenPlace = 4 - takenPlace;
                } else {
                    takenPlace = PlaceConditions.UnremarkablePlace;
                }
            }
            TakenPlace = takenPlace;

            if (KunosCareerType == KunosCareerObjectType.SingleEvents) {
                IsAvailable = true;
                IsPassed = false;
            } else {
                IsAvailable = TakenPlace == 0 && entry.SelectedEvent == EventNumber;
                IsPassed = TakenPlace != 0;
            }
        }

        public override void SaveData(IniFile ini) {
            ini["EVENT"].Set("NAME", Name);
            ini["EVENT"].Set("DESCRIPTION", AcStringValues.EncodeDescription(Description));
            throw new NotImplementedException();
        }

        public override int CompareTo(AcPlaceholderNew o) {
            var c = o as KunosCareerEventObject;
            return c == null ? base.CompareTo(o) : AlphanumComparatorFast.Compare(Id, c.Id);
        }

        private RelayCommand _resetUserAiLevelCommand;

        public RelayCommand ResetUserAiLevelCommand => _resetUserAiLevelCommand ?? (_resetUserAiLevelCommand = new RelayCommand(o => {
            UserAiLevel = AiLevel;
        }, o => UserAiLevel != AiLevel));

        private IniFile ConvertConfig(string original) {
            var iniFile = IniFile.Parse(original);
            iniFile.Remove("EVENT");
            iniFile.Remove("SPECIAL_EVENT");
            iniFile.RemoveSections("CONDITION");

            iniFile["RACE"].Set("AI_LEVEL", SettingsHolder.Drive.KunosCareerUserAiLevel ? UserAiLevel : AiLevel);
            if (SettingsHolder.Drive.KunosCareerUserSkin) {
                iniFile["RACE"].SetId("SKIN", CarSkin.Id);
                iniFile["CAR_0"].SetId("SKIN", CarSkin.Id);
            }

            var trackProperties = Game.DefaultTrackPropertiesPresets.ElementAtOrDefault(iniFile["DYNAMIC_TRACK"].GetInt("PRESET", -1)) ??
                    Game.GetDefaultTrackPropertiesPreset();
            trackProperties.Properties.Set(iniFile);

            iniFile["RACE"].SetId("MODEL", iniFile["RACE"].Get("MODEL"));
            iniFile["RACE"].SetId("SKIN", iniFile["RACE"].Get("SKIN"));
            iniFile["RACE"].SetId("TRACK", iniFile["RACE"].Get("TRACK"));

            iniFile["CAR_0"].SetId("MODEL", iniFile["CAR_0"].Get("MODEL"));
            iniFile["CAR_0"].SetId("SKIN", iniFile["CAR_0"].Get("SKIN"));
            iniFile["CAR_0"].Set("DRIVER_NAME", SettingsHolder.Drive.PlayerName);
            iniFile["CAR_0"].Set("NATIONALITY", SettingsHolder.Drive.PlayerNationality);

            IniFile opponentsIniFile = null;
            foreach (var i in Enumerable.Range(0, iniFile["RACE"].GetInt("CARS", 0)).Skip(1)) {
                var sectionKey = "CAR_" + i;
                if (!iniFile.ContainsKey(sectionKey) || string.IsNullOrWhiteSpace(iniFile[sectionKey].Get("DRIVER_NAME"))) {
                    if (opponentsIniFile == null) {
                        var career = KunosCareerManager.Instance.GetById(KunosCareerId);
                        if (career == null) throw new Exception("Can’t find parent career with ID=" + KunosCareerId);

                        opponentsIniFile = new IniFile(career.OpponentsIniFilename);
                        if (opponentsIniFile.IsEmptyOrDamaged()) break;
                    }

                    iniFile[sectionKey] = opponentsIniFile["AI" + i];
                }

                iniFile[sectionKey].SetId("MODEL", iniFile[sectionKey].Get("MODEL"));
                iniFile[sectionKey].SetId("SKIN", iniFile[sectionKey].Get("SKIN"));
            }

            return iniFile;
        }

        private RelayPropertyCommand _goCommand;

        public RelayPropertyCommand GoCommand => _goCommand ?? (_goCommand = new RelayPropertyCommand(async o => {
            await GameWrapper.StartAsync(new Game.StartProperties {
                AdditionalPropertieses = {
                    ConditionType.HasValue ? new PlaceConditions {
                        Type = ConditionType.Value,
                        FirstPlaceTarget = FirstPlaceTarget,
                        SecondPlaceTarget = SecondPlaceTarget,
                        ThirdPlaceTarget = ThirdPlaceTarget
                    } : null,
                    new KunosCareerManager.CareerProperties { CareerId = KunosCareerId, EventId = Id }
                },
                PreparedConfig = ConvertConfig(FileUtils.ReadAllText(IniFilename)),
                AssistsProperties = o as Game.AssistsProperties
            });
        }, o => IsAvailable));

        public void ResetSkinToDefault() {
            if (!string.Equals(CarSkin?.Id, CarSkinId, StringComparison.OrdinalIgnoreCase)) {
                CarSkin = Car.GetSkinByIdFromConfig(CarSkinId);
            }
        }
    }
}
