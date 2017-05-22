using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public abstract class KunosEventObjectBase : AcIniObject {
        public static bool OptionIgnoreMissingSkins = true;

        protected KunosEventObjectBase(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {}

        protected sealed override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "event.ini");
            PreviewImage = Path.Combine(Location, "preview.png");
        }

        public string PreviewImage { get; private set; }

        public sealed override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) return true;

            if (FileUtils.IsAffected(filename, PreviewImage)) {
                OnImageChangedValue(PreviewImage);
                return true;
            }

            return false;
        }

        public sealed override void Reload() {
            OnImageChangedValue(PreviewImage);
            base.Reload();
        }

        public override int CompareTo(AcPlaceholderNew o) {
            return Enabled == o.Enabled ?
                    AlphanumComparatorFast.Compare(Id, o.Id) : Enabled ? -1 : 1;
        }

        private string _description;

        public string Description {
            get { return _description; }
            set {
                if (Equals(value, _description)) return;
                _description = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private string _displayType;

        public string DisplayType {
            get { return _displayType; }
            set {
                if (Equals(value, _displayType)) return;
                _displayType = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private string _trackConfigurationId;

        public string TrackConfigurationId {
            get { return _trackConfigurationId; }
            set {
                if (Equals(value, _trackConfigurationId)) return;
                _trackConfigurationId = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private string _carId;

        public string CarId {
            get { return _carId; }
            set {
                if (Equals(value, _carId)) return;
                _carId = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private string _carSkinId;

        public string CarSkinId {
            get { return _carSkinId; }
            set {
                if (Equals(value, _carSkinId)) return;
                _carSkinId = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private string _weatherId;

        public string WeatherId {
            get { return _weatherId; }
            set {
                if (Equals(value, _weatherId)) return;
                _weatherId = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private PlaceConditionsType? _conditionType;

        /// <summary>
        /// Champinoship � null, otherwise has value.
        /// </summary>
        public PlaceConditionsType? ConditionType {
            get { return _conditionType; }
            set {
                if (Equals(value, _conditionType)) return;
                _conditionType = value;

                if (Loaded) {
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string ToDisplayPlaceTarget(int? value) {
            return value == null ? null : ConditionType == PlaceConditionsType.Time
                    ? TimeSpan.FromMilliseconds(value.Value).ToMillisecondsString() : value.Value.ToString();
        }

        private int? _firstPlaceTarget;

        public int? FirstPlaceTarget {
            get { return _firstPlaceTarget; }
            set {
                if (Equals(value, _firstPlaceTarget)) return;
                _firstPlaceTarget = value;
                DisplayFirstPlaceTarget = ToDisplayPlaceTarget(value);

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayFirstPlaceTarget));
                }
            }
        }

        public string DisplayFirstPlaceTarget { get; private set; }

        private int? _secondPlaceTarget;

        public int? SecondPlaceTarget {
            get { return _secondPlaceTarget; }
            set {
                if (Equals(value, _secondPlaceTarget)) return;
                _secondPlaceTarget = value;
                DisplaySecondPlaceTarget = ToDisplayPlaceTarget(value);

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplaySecondPlaceTarget));
                }
            }
        }

        public string DisplaySecondPlaceTarget { get; private set; }

        private int? _thirdPlaceTarget;

        public int? ThirdPlaceTarget {
            get { return _thirdPlaceTarget; }
            set {
                if (Equals(value, _thirdPlaceTarget)) return;
                _thirdPlaceTarget = value;
                DisplayThirdPlaceTarget = ToDisplayPlaceTarget(value);

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayThirdPlaceTarget));
                }
            }
        }

        public string DisplayThirdPlaceTarget { get; private set; }

        private int _time;

        public int Time {
            get { return _time; }
            set {
                if (Equals(value, _time)) return;
                _time = value;

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public string DisplayTime {
            get { return $@"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}"; }
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

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private int _opponentsCount;

        public int OpponentsCount {
            get { return _opponentsCount; }
            set {
                if (Equals(value, _opponentsCount)) return;
                _opponentsCount = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private int? _laps;

        public int? Laps {
            get { return _laps; }
            set {
                if (Equals(value, _laps)) return;
                _laps = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private int _aiLevel;

        public int AiLevel {
            get { return _aiLevel; }
            set {
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        #region Loadable objects
        private TrackObjectBase _trackObject;

        public TrackObjectBase TrackObject {
            get { return _trackObject; }
            set {
                if (Equals(value, _trackObject)) return;
                _trackObject = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private CarObject _carObject;

        public CarObject CarObject {
            get { return _carObject; }
            set {
                if (Equals(value, _carObject)) return;
                _carObject = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private CarSkinObject _carSkin;

        public CarSkinObject CarSkin {
            get { return _carSkin; }
            set {
                if (Equals(value, _carSkin)) return;
                _carSkin = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private WeatherObject _weatherObject;

        public WeatherObject WeatherObject {
            get { return _weatherObject; }
            set {
                if (Equals(value, _weatherObject)) return;
                _weatherObject = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private double _temperature;

        public double Temperature {
            get { return _temperature; }
            set {
                if (Equals(value, _temperature)) return;
                _temperature = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private double _roadTemperature;

        public double RoadTemperature {
            get { return _roadTemperature; }
            set {
                if (Equals(value, _roadTemperature)) return;
                _roadTemperature = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }

        private Game.TrackPropertiesPreset _trackPreset;

        public Game.TrackPropertiesPreset TrackPreset {
            get { return _trackPreset; }
            set {
                if (Equals(value, _trackPreset)) return;
                _trackPreset = value;

                if (Loaded) {
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        protected virtual void LoadObjects() {
            TrackObject = TrackId == null ? null : TracksManager.Instance.GetLayoutById(TrackId, TrackConfigurationId);
            CarObject = CarId == null ? null : CarsManager.Instance.GetById(CarId);
            WeatherObject = WeatherManager.Instance.GetById(WeatherId ?? string.Empty);

            ErrorIf(TrackObject == null, AcErrorType.Data_KunosCareerTrackIsMissing, TrackId);
            ErrorIf(CarObject == null, AcErrorType.Data_KunosCareerCarIsMissing, CarId);

            if (CarObject != null) {
                CarSkin = CarSkinId == null ? null : CarObject.GetSkinByIdFromConfig(CarSkinId);

                if (!OptionIgnoreMissingSkins) {
                    ErrorIf(CarSkin == null, AcErrorType.Data_KunosCareerCarSkinIsMissing, CarId, CarSkinId);
                }

                if (CarSkin == null) {
                    CarSkin = (CarSkinObject)CarObject.SkinsManager.WrappersList.RandomElementOrDefault()?.Loaded();
                }
            } else {
                RemoveError(AcErrorType.Data_KunosCareerCarSkinIsMissing);
            }

            ErrorIf(WeatherObject == null, AcErrorType.Data_KunosCareerWeatherIsMissing, WeatherId);
        }

        protected virtual void LoadConditions(IniFile ini) {
            var conditions = LinqExtension.RangeFrom()
                    .Select(x => $@"CONDITION_{x}")
                    .TakeWhile(ini.ContainsKey)
                    .Select(x => new {
                        Type = ini[x].GetEnumNullable<PlaceConditionsType>("TYPE"),
                        Value = ini[x].GetIntNullable("OBJECTIVE")
                    })
                    .ToList();

            if (conditions.Count != 3 || conditions[0].Type == null ||
                    conditions.Any(x => x.Value == null || x.Type != null && x.Type != conditions[0].Type)) {
                AddError(AcErrorType.Data_KunosCareerConditions, ini["CONDITION_0"].GetNonEmpty("TYPE") ?? @"?");
            } else {
                RemoveError(AcErrorType.Data_KunosCareerConditions);
                ConditionType = conditions[0].Type.Value;
                FirstPlaceTarget = conditions[2].Value.Value;
                SecondPlaceTarget = conditions[1].Value.Value;
                ThirdPlaceTarget = conditions[0].Value.Value;
            }
        }

        public abstract void LoadProgress();

        protected override void LoadData(IniFile ini) {
            Name = ini["EVENT"].GetPossiblyEmpty("NAME");
            Description = AcStringValues.DecodeDescription(ini["EVENT"].GetPossiblyEmpty("DESCRIPTION"));

            TrackId = ini["RACE"].GetNonEmpty("TRACK");
            TrackConfigurationId = ini["RACE"].GetNonEmpty("CONFIG_TRACK");
            CarId = ini["RACE"].GetNonEmpty("MODEL");
            CarSkinId = ini["CAR_0"].GetNonEmpty("SKIN");
            WeatherId = ini["WEATHER"].GetNonEmpty("NAME") ?? WeatherManager.Instance.GetDefault()?.Id;

            Time = (int)Game.ConditionProperties.GetSeconds(ini["LIGHTING"].GetInt("SUN_ANGLE", 40));
            Temperature = ini["TEMPERATURE"].GetDouble("AMBIENT", 26);
            RoadTemperature = ini["TEMPERATURE"].GetDouble("ROAD", 32);

            TrackPreset = Game.DefaultTrackPropertiesPresets.GetByIdOrDefault(ini["DYNAMIC_TRACK"].GetIntNullable("PRESET")) ??
                    Game.DefaultTrackPropertiesPresets[4];
            DisplayType = ini.ContainsKey(@"SESSION_1") ? ToolsStrings.Common_Weekend :
                    (ini["SESSION_0"].GetNonEmpty("NAME")?.Replace(@" Session", "") ?? ToolsStrings.Session_Race);

            StartingPosition = ini["SESSION_0"].GetIntNullable("STARTING_POSITION");
            OpponentsCount = ini["RACE"].GetInt("CARS", 1) - 1;

            if (OpponentsCount > 0 && StartingPosition == null) {
                StartingPosition = OpponentsCount + 1;
            }

            if (StartingPosition != null || ini.ContainsKey(@"SESSION_1")) {
                Laps = ini["SESSION_0"].GetIntNullable("LAPS") ?? ini["RACE"].GetIntNullable("RACE_LAPS") ?? 0;
            } else {
                Laps = null;
            }

            AiLevel = ini["RACE"].GetInt("AI_LEVEL", 100);

            LoadObjects();
            LoadConditions(ini);
            LoadProgress();
        }

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

                if (Loaded) {
                    OnPropertyChanged();
                    TakenPlaceChanged();
                }
            }
        }

        protected virtual void TakenPlaceChanged() {}
        #endregion

        public void ResetSkinToDefault() {
            if (!string.Equals(CarSkin?.Id, CarSkinId, StringComparison.OrdinalIgnoreCase)) {
                CarSkin = CarObject.GetSkinByIdFromConfig(CarSkinId);
            }
        }

        public override void SaveData(IniFile ini) {
            throw new NotSupportedException();
        }

        protected virtual void SetCustomSkinId(IniFile ini) {
            ini["RACE"].SetId("SKIN", CarSkin.Id);
            ini["CAR_0"].SetId("SKIN", CarSkin.Id);
        }

        protected virtual IniFile ConvertConfig(IniFile ini) {
            // iniFile.Remove(@"EVENT");
            // iniFile.Remove(@"SPECIAL_EVENT");
            // iniFile.RemoveSections("CONDITION");

            SetCustomSkinId(ini);

            var trackProperties = Game.DefaultTrackPropertiesPresets.ElementAtOrDefault(ini["DYNAMIC_TRACK"].GetInt("PRESET", -1)) ??
                    Game.GetDefaultTrackPropertiesPreset();
            trackProperties.Properties.Set(ini);

            ini["RACE"].SetId("MODEL", ini["RACE"].GetPossiblyEmpty("MODEL"));
            ini["RACE"].SetId("SKIN", ini["RACE"].GetPossiblyEmpty("SKIN"));
            ini["RACE"].SetId("TRACK", ini["RACE"].GetPossiblyEmpty("TRACK"));

            ini["CAR_0"].SetId("MODEL", ini["CAR_0"].GetPossiblyEmpty("MODEL"));
            ini["CAR_0"].SetId("SKIN", ini["CAR_0"].GetPossiblyEmpty("SKIN"));
            ini["CAR_0"].Set("DRIVER_NAME", SettingsHolder.Drive.PlayerName);
            ini["CAR_0"].Set("NATIONALITY", SettingsHolder.Drive.PlayerNationality);
            ini["CAR_0"].Set("NATION_CODE", NationCodeProvider.Instance.GetNationCode(SettingsHolder.Drive.PlayerNationality));
            return ini;
        }
    }
}