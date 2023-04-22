using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Custom : IQuickDriveModeControl {
        public QuickDrive_Custom() {
            InitializeComponent();
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            ActualModel.Load();
            if (ActualModel.AiLimit == 0 && Wrapper.ColumnDefinitions.Count == 2) {
                Wrapper.ColumnDefinitions.RemoveAt(1);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            ActualModel.Unload();
        }

        public QuickDriveModeViewModel Model {
            get => ActualModel;
            set => DataContext = value;
        }

        public ViewModel ActualModel => (ViewModel)DataContext;

        public class ViewModel : QuickDrive_Race.ViewModel {
            public string ID { get; }
            
            protected override bool IgnoreStartingPosition => true;

            private enum SessionType {
                Practice,
                TrackDay,
                Race,
            }

            private readonly NewRaceModeData _mode;
            private readonly SessionType _sessionType;
            private readonly int _aiLevel = -1;
            private readonly int _startingPosition = -1;
            private readonly double _speedLimit = 0d;
            private readonly Game.StartType _selectedStartType;
            
            public int AiLimit { get; }
            
            public string DetailedDescription { get; set; }
            
            public bool ShowPenalties { get; set; }
            
            public bool ShowLapsNumber { get; set; }
            
            public bool ShowAiLevel { get; set; }
            
            public bool ShowJumpStartPenalty { get; set; }
            
            public bool ShowStartingPositionSelection { get; set; }
            
            // public bool ShowBallast { get; set; }
            // public bool ShowRestrictor { get; set; }

            private readonly Tuple<string, string> _filterCarsRaw;
            private readonly Tuple<string, string> _filterTracksRaw;
            private readonly IFilter<CarObject> _filterCars;
            private readonly IFilter<TrackObjectBase> _filterTracks;

            public override Tuple<string, string> GetDefaultCarFilter() {
                return _filterCarsRaw;
            }

            public override Tuple<string, string> GetDefaultTrackFilter() {
                return _filterTracksRaw;
            }

            private Tuple<string, string> SplitFilter(string filter) {
                var i = filter.IndexOf('@');
                return i != -1 ? Tuple.Create(filter.Substring(0, i).TrimEnd(), filter.Substring(i + 1).TrimStart()) 
                        : Tuple.Create(filter, string.Empty);
            }

            public ViewModel(string id, bool initialize = true) : base(null) {
                ID = id;
                _mode = NewRaceModeData.Instance.Items.GetByIdOrDefault(id) ?? throw new Exception($"Mode {id} is missing");

                var manifest = new IniFile(Path.Combine(_mode.Location, "manifest.ini"));
                switch (manifest["RULES"].GetNonEmpty("BASE_MODE", "PRACTICE")) {
                    case "TRACK_DAY":
                        _sessionType = SessionType.TrackDay;
                        break;
                    case "RACE":
                        _sessionType = SessionType.Race;
                        break;
                    default:
                        _sessionType = SessionType.Practice;
                        break;
                }

                var carFilter = manifest["RULES"].GetNonEmpty("ALLOWED_CARS");
                if (carFilter != null && carFilter != @"*") {
                    _filterCarsRaw = SplitFilter(carFilter);
                    _filterCars = Filter.Create(CarObjectTester.Instance, _filterCarsRaw.Item1);
                }

                var trackFilter = manifest["RULES"].GetNonEmpty("ALLOWED_TRACKS");
                if (trackFilter != null && trackFilter != @"*") {
                    _filterTracksRaw = SplitFilter(trackFilter);
                    _filterTracks = Filter.Create(TrackObjectBaseTester.Instance, _filterTracksRaw.Item1);
                }
                
                var penalties = manifest["RULES"].GetInt("PENALTIES", -1);
                if (penalties == -1) {
                    ShowPenalties = true;
                } else {
                    Penalties = penalties == 1;
                }

                if (_sessionType == SessionType.Practice) {
                    AiLimit = 0;

                    var startType = manifest["RULES"].GetNonEmpty("START_TYPE");
                    if (startType != null) {
                        _selectedStartType = Game.StartType.Values.GetByIdOrDefault(startType) ?? Game.StartType.Pit;
                    } else {
                        _selectedStartType = Game.StartType.Pit;
                    }
                } else {
                    AiLimit = manifest["RULES"].GetInt("AI_LIMIT", int.MaxValue);
                    if (AiLimit > 0) {
                        var aiLevel = manifest["RULES"].GetInt("AI_LEVEL", -1);
                        if (aiLevel == -1) {
                            ShowAiLevel = true;
                        } else {
                            _aiLevel = aiLevel;
                        }
                    }

                    if (_sessionType == SessionType.Race) {
                        var lapsNumber = manifest["RULES"].GetInt("LAPS_COUNT", -1);
                        if (lapsNumber > 0) {
                            LapsNumber = lapsNumber;
                        } else {
                            ShowLapsNumber = true;
                        }

                        var jumpStartPenalty = manifest["RULES"].GetNonEmpty("JUMP_START_PENALTY");
                        if (jumpStartPenalty != null) {
                            JumpStartPenalty = (Game.JumpStartPenaltyType)Enum.Parse(typeof(Game.JumpStartPenaltyType), jumpStartPenalty, true);
                        } else {
                            ShowJumpStartPenalty = true;
                        }

                        var startingPosition = manifest["RULES"].GetInt("STARTING_POSITION", -1);
                        if (startingPosition == -1) {
                            ShowStartingPositionSelection = true;
                        } else {
                            _startingPosition = startingPosition;
                        }
                    }

                    if (_sessionType == SessionType.TrackDay) {
                        _speedLimit = manifest["RULES"].GetDouble("SPEED_LIMIT", 0d);
                    }
                }

                LoadSaveable(initialize);

                try {
                    var detailsFilename = Path.Combine(_mode.Location, "details.txt");
                    if (File.Exists(detailsFilename)) {
                        DetailedDescription = File.ReadAllText(detailsFilename).TrimEnd();
                    }
                } catch {
                    // ignored
                }
                
                InitializeConfig();
            }

            [CanBeNull]
            public PythonAppConfig Config { get; set; }

            private string _settingsKey;
            
            private void InitializeConfig() {
                _settingsKey = $@".new-mode.{_mode.Id}";
                Config = new PythonAppConfigs(new PythonAppConfigParams(_mode.Location) {
                    FilesRelativeDirectory = AcRootDirectory.Instance.RequireValue,
                    ScanFunc = d => Directory.GetFiles(d, "settings.ini"),
                    ConfigFactory = (p, f) => {
                        try {
                            return PythonAppConfig.Create(p, f, true, _mode.GetUserSettingsFilename());
                        } catch (Exception e) {
                            Logging.Warning(e);
                            return null;
                        }
                    },
                    SaveOnlyNonDefault = true,
                }).FirstOrDefault();

                if (Config != null) {
                    var settings = ValuesStorage.Get<string>(_settingsKey);
                    if (!string.IsNullOrEmpty(settings)) {
                        Config.Import(settings);
                    }
                    var busy = new Busy();
                    Config.ValueChanged += (sender, args) => busy.DoDelay(OnConfigChange, 200);
                }
            }

            private void OnConfigChange() {
                if (Config == null) return;
                ValuesStorage.Set(_settingsKey, Config.Serialize());
            }

            private new class SaveableData : QuickDrive_Race.ViewModel.SaveableData {
                [CanBeNull]
                public string Data;
            }

            protected override void Save(QuickDrive_Race.ViewModel.SaveableData result) {
                base.Save(result);
                var r = (SaveableData)result;
                r.Data = Config?.Serialize();
            }

            protected override void Load(QuickDrive_Race.ViewModel.SaveableData o) {
                base.Load(o);
                if (AiLimit > 0 && AiLimit < int.MaxValue) {
                    RaceGridViewModel.OpponentsNumber = AiLimit;
                }
                var r = (SaveableData)o;
                if (!string.IsNullOrEmpty(r.Data)) Config?.Import(r.Data);
            }

            protected override void Reset() {
                base.Reset();
                if (AiLimit > 0 && AiLimit < int.MaxValue) {
                    RaceGridViewModel.OpponentsNumber = AiLimit;
                }
                Config?.Import(string.Empty);
            }

            protected override void CheckIfCarFits(CarObject track) {
                CarDoesNotFit = track != null && _filterCars?.Test(track) == false 
                        ? Tuple.Create("Car is not for this mode", EmptyCarAction) : null;
            }

            protected override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = track != null && _filterTracks?.Test(track) == false 
                        ? Tuple.Create("Track is not for this mode", EmptyTrackAction) : null;
            }

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Custom:" + ID, () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
            }

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties,
                    string serializedQuickDrivePreset, IList<object> additionalProperties) {
                if (_aiLevel != -1) {
                    RaceGridViewModel.AiLevel = _aiLevel;
                }
                if (_startingPosition != -1) {
                    RaceGridViewModel.StartingPosition = _startingPosition;
                }
                
                IEnumerable<Game.AiCar> botCars = null;
                if (_sessionType != SessionType.Practice) {
                    try {
                        var selectedCar = CarsManager.Instance.GetById(basicProperties.CarId ?? "");
                        var selectedTrack = TracksManager.Instance.GetLayoutById(basicProperties.TrackId ?? "", basicProperties.TrackConfigurationId);
                        using (var waiting = new WaitingDialog()) {
                            if (selectedCar == null || !selectedCar.Enabled) {
                                ModernDialog.ShowMessage(AppStrings.Drive_CannotStart_SelectNonDisabled, AppStrings.Drive_CannotStart_Title, MessageBoxButton.OK);
                                return;
                            }

                            if (selectedTrack == null) {
                                ModernDialog.ShowMessage(AppStrings.Drive_CannotStart_SelectTrack, AppStrings.Drive_CannotStart_Title, MessageBoxButton.OK);
                                return;
                            }

                            botCars = await RaceGridViewModel.GenerateGameEntries(waiting.CancellationToken);
                            if (waiting.CancellationToken.IsCancellationRequested) return;

                            if (botCars == null || !botCars.Any()) {
                                ModernDialog.ShowMessage(AppStrings.Drive_CannotStart_SetOpponent, AppStrings.Drive_CannotStart_Title, MessageBoxButton.OK);
                                return;
                            }
                        }
                    } catch (Exception e) when (e.IsCancelled()) {
                        return;
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t create race grid", e);
                        return;
                    }
                }

                basicProperties.Ballast = RaceGridViewModel.PlayerBallast;
                basicProperties.Restrictor = RaceGridViewModel.PlayerRestrictor;

                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = GetModeProperties(botCars),
                    AdditionalPropertieses = additionalProperties.Concat(new object[] {
                        new QuickDrivePresetProperty(serializedQuickDrivePreset),
                        new CarCustomDataHelper(),
                        new CarExtendedPhysicsHelper(),
                        new NewModeDetails(_mode.Id),
                    }).ToList()
                });
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                _mode.PublishSettings(Config?.Serialize());
                if (_sessionType == SessionType.Race) {
                    return new Game.RaceProperties {
                        AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                        Penalties = Penalties,
                        JumpStartPenalty = JumpStartPenalty,
                        StartingPosition = RaceGridViewModel.StartingPositionLimited == 0
                                ? MathUtils.Random(1, RaceGridViewModel.OpponentsNumberLimited + 2) : RaceGridViewModel.StartingPositionLimited,
                        RaceLaps = LapsNumber,
                        BotCars = botCars
                    };
                }
                if (_sessionType == SessionType.TrackDay) {
                    return new Game.TrackdayProperties {
                        AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                        Penalties = Penalties,
                        JumpStartPenalty = Game.JumpStartPenaltyType.None,
                        StartingPosition = 1,
                        RaceLaps = LapsNumber,
                        BotCars = botCars,
                        UsePracticeSessionType = SettingsHolder.Drive.QuickDriveTrackDayViaPractice,
                        SpeedLimit = _speedLimit
                    };
                }
                return new Game.PracticeProperties {
                    Penalties = Penalties,
                    StartType = _selectedStartType
                };
            }
        }
    }
}
