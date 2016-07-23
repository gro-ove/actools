using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using StringBasedFilter;
using WaitingDialog = AcManager.Controls.Dialogs.WaitingDialog;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Race : IQuickDriveModeControl {
        public QuickDrive_Race() {
            InitializeComponent();
            // DataContext = new QuickDrive_RaceViewModel();
        }

        private bool _loaded;

        private void QuickDrive_Race_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            ActualModel.Load();
        }

        private void QuickDrive_Race_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            ActualModel.Unload();
        }


        public QuickDriveModeViewModel Model {
            get { return (QuickDriveModeViewModel)DataContext; }
            set { DataContext = value; }
        }

        public ViewModel ActualModel => (ViewModel)DataContext;

        public class ViewModel : QuickDriveModeViewModel, IComparer {
            private bool _penalties;

            public bool Penalties {
                get { return _penalties; }
                set {
                    if (value == _penalties) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _jumpStartPenalty;

            public bool JumpStartPenalty {
                get { return _jumpStartPenalty; }
                set {
                    if (Equals(value, _jumpStartPenalty)) return;
                    _jumpStartPenalty = value;
                    OnPropertyChanged();
                }
            }

            public int LapsNumberMaximum => SettingsHolder.Drive.QuickDriveExpandBounds ? 999 : 40;

            public int LapsNumberMaximumLimited => Math.Min(LapsNumberMaximum, 50);

            public int AiLevelMinimum => SettingsHolder.Drive.QuickDriveExpandBounds ? 30 : 70;

            public int AiLevelMinimumLimited => Math.Max(AiLevelMinimum, 50);

            private int _lapsNumber;

            public int LapsNumber {
                get { return _lapsNumber; }
                set {
                    if (Equals(value, _lapsNumber)) return;
                    _lapsNumber = value.Clamp(1, LapsNumberMaximum);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private int _opponentsNumber;
            private int _unclampedOpponentsNumber;
            private int? _unclampedStartingPosition;

            public int OpponentsNumber {
                get { return _opponentsNumber; }
                set {
                    if (Equals(value, _opponentsNumber)) return;

                    _unclampedOpponentsNumber = value;
                    _opponentsNumber = value.Clamp(1, OpponentsNumberLimit);

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StartingPositionLimit));

                    if (_unclampedStartingPosition.HasValue && _unclampedStartingPosition != StartingPosition) {
                        StartingPosition = _unclampedStartingPosition.Value;
                    }

                    if (_last || StartingPosition > StartingPositionLimit) {
                        _innerChange = true;
                        StartingPosition = StartingPositionLimit;
                        _innerChange = false;
                    } else if (StartingPosition == StartingPositionLimit && StartingPositionLimit != 0) {
                        _last = true;
                        OnPropertyChanged(nameof(DisplayStartingPosition));
                    }

                    SaveLater();
                }
            }

            private int _aiLevel;

            public int AiLevel {
                get { return _aiLevel; }
                set {
                    value = value.Clamp(AiLevelMinimum, 100);
                    if (Equals(value, _aiLevel)) return;
                    _aiLevel = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (!AiLevelFixed && value >= AiLevelMin) return;
                    _aiLevelMin = value;
                    OnPropertyChanged(nameof(AiLevelMin));
                }
            }

            private int _aiLevelMin;

            public int AiLevelMin {
                get { return _aiLevelMin; }
                set {
                    if (AiLevelFixed) return;

                    value = value.Clamp(AiLevelMinimum, 100);
                    if (Equals(value, _aiLevelMin)) return;
                    _aiLevelMin = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (value > AiLevel) {
                        _aiLevel = value;
                        OnPropertyChanged(nameof(AiLevel));
                    }
                }
            }

            private bool _aiLevelFixed;

            public bool AiLevelFixed {
                get { return _aiLevelFixed; }
                set {
                    if (Equals(value, _aiLevelFixed)) return;
                    _aiLevelFixed = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (value && _aiLevelMin != _aiLevel) {
                        _aiLevelMin = _aiLevel;
                        OnPropertyChanged(nameof(AiLevelMin));
                    }
                }
            }

            private bool _aiLevelArrangeRandomly;

            public bool AiLevelArrangeRandomly {
                get { return _aiLevelArrangeRandomly; }
                set {
                    if (Equals(value, _aiLevelArrangeRandomly)) return;
                    _aiLevelArrangeRandomly = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (value) {
                        AiLevelArrangeReverse = false;
                    }
                }
            }

            private bool _aiLevelArrangeReverse;

            public bool AiLevelArrangeReverse {
                get { return _aiLevelArrangeReverse; }
                set {
                    if (Equals(value, _aiLevelArrangeReverse)) return;
                    _aiLevelArrangeReverse = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (value) {
                        AiLevelArrangeRandomly = false;
                    }
                }
            }

            private bool _last;
            private int _startingPosition;
            private bool _innerChange;

            public int StartingPosition {
                get { return _startingPosition; }
                set {
                    if (!_innerChange) {
                        _unclampedStartingPosition = Math.Max(value, 0);
                    }

                    value = value.Clamp(0, StartingPositionLimit);
                    if (Equals(value, _startingPosition)) return;

                    _startingPosition = value;
                    _last = value == StartingPositionLimit && StartingPositionLimit != 0;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayStartingPosition));
                    SaveLater();
                }
            }

            public string DisplayStartingPosition => StartingPosition == 0 ? @"Random" : _last ? @"Last" : StartingPosition.ToOrdinal("driver");

            public int StartingPositionLimit => OpponentsNumber + 1;

            private int _trackPitsNumber;

            public int TrackPitsNumber {
                get { return _trackPitsNumber; }
                set {
                    if (Equals(value, _trackPitsNumber)) return;
                    _trackPitsNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpponentsNumberLimit));

                    if (_unclampedOpponentsNumber != _opponentsNumber) {
                        OpponentsNumber = _unclampedOpponentsNumber;
                    } else if (OpponentsNumber > OpponentsNumberLimit) {
                        OpponentsNumber = OpponentsNumberLimit;
                    }
                }
            }

            public int OpponentsNumberLimit => TrackPitsNumber - 1;

            protected class SaveableData {
                public bool? Penalties, AiLevelFixed, AiLevelArrangeRandomly, JumpStartPenalty, AiLevelArrangeReverse;
                public int? AiLevel, AiLevelMin, LapsNumber, OpponentsNumber, StartingPosition;
                public string GridTypeId, OpponentsCarsFilter;
                public string[] ManualList;
            }

            protected virtual void Save(SaveableData result) {
                result.Penalties = Penalties;
                result.JumpStartPenalty = JumpStartPenalty;
                result.AiLevelFixed = AiLevelFixed;
                result.AiLevelArrangeRandomly = AiLevelArrangeRandomly;
                result.AiLevelArrangeReverse = AiLevelArrangeReverse;
                result.AiLevel = AiLevel;
                result.AiLevelMin = AiLevelMin;
                result.LapsNumber = LapsNumber;
                result.OpponentsNumber = OpponentsNumber;
                result.StartingPosition = StartingPosition;
                result.GridTypeId = SelectedGridType?.Id;
                result.OpponentsCarsFilter = OpponentsCarsFilter;
                result.ManualList = _opponentsCarsIds ?? (SelectedGridType == GridType.Manual ? OpponentsCars.Select(x => x.Id).ToArray() : null);
            }

            protected virtual void Load(SaveableData o) {
                Penalties = o.Penalties ?? true;
                JumpStartPenalty = o.JumpStartPenalty ?? false;
                AiLevelFixed = o.AiLevelFixed ?? true;
                AiLevelArrangeRandomly = o.AiLevelArrangeRandomly ?? true;
                AiLevelArrangeReverse = o.AiLevelArrangeReverse ?? false;
                AiLevel = o.AiLevel ?? 92;
                AiLevelMin = o.AiLevelMin ?? 92;
                LapsNumber = o.LapsNumber ?? 2;
                OpponentsNumber = o.OpponentsNumber ?? 3;
                StartingPosition = o.StartingPosition ?? 4;

                UpdateGridTypes();
                SelectedGridType = GridTypes.GetByIdOrDefault(o.GridTypeId) ?? GridType.SameCar;

                _opponentsCarsIds = SelectedGridType == GridType.Manual ? o.ManualList : null;
                OpponentsCarsFilter = o.OpponentsCarsFilter;
            }

            protected virtual void Reset() {
                Penalties = true;
                JumpStartPenalty = false;
                AiLevelFixed = true;
                AiLevelArrangeRandomly = true;
                AiLevelArrangeReverse = false;
                AiLevel = 92;
                AiLevelMin = 92;
                LapsNumber = 2;
                OpponentsNumber = 3;
                StartingPosition = 4;
                SelectedGridType = GridType.SameCar;
                OpponentsCarsFilter = string.Empty;
            }

            /// <summary>
            /// Will be called in constuctor!
            /// </summary>
            protected virtual void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Race", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
            }

            public ViewModel(bool initialize = true) {
                OpponentsCars = new BetterObservableCollection<CarObject>();
                OpponentsCarsView = new BetterListCollectionView(OpponentsCars) { CustomSort = this };

                // ReSharper disable once VirtualMemberCallInContructor
                InitializeSaveable();

                if (initialize) {
                    Saveable.LoadOrReset();
                } else {
                    Saveable.Reset();
                }
            }

            public class GridType : IWithId {
                public static readonly GridType SameCar = new GridType("Same car");
                public static readonly GridType SameGroup = new GridType("Same group");
                public static readonly GridType FilteredBy = new GridType("Filtered by…");
                public static readonly GridType Manual = new GridType("Manual");

                [JsonProperty(PropertyName = @"id")]
                private string _id;

                public string Id => _id ?? (_id = AcStringValues.IdFromName(DisplayName));

                [JsonProperty(PropertyName = @"name")]
                private readonly string _displayName;

                public string DisplayName => _displayName;

                [JsonProperty(PropertyName = @"filter")]
                private readonly string _filter;

                public string Filter => _filter;

                [JsonProperty(PropertyName = @"script")]
                private readonly string _script;

                public string Script => _script;

                [JsonProperty(PropertyName = @"test")]
                private readonly bool _test;

                public bool Test => _test;

                public override string ToString() => Id;

                [JsonConstructor]
                // ReSharper disable once UnusedMember.Local
                private GridType() { }

                private GridType(string displayName) {
                    _displayName = displayName;
                    _filter = "";
                    _script = "";
                    _test = false;
                }
            }

            public BetterObservableCollection<GridType> GridTypes { get; } = new BetterObservableCollection<GridType> {
                GridType.SameCar,
                GridType.SameGroup
            };

            public void UpdateGridTypes() {
                var selectedId = SelectedGridType?.Id;
                GridTypes.ReplaceEverythingBy(new[] { GridType.SameCar, GridType.SameGroup }
                        .Union(FilesStorage.Instance.LoadJsonContentFile<GridType[]>(ContentCategory.GridTypes, "GridTypes.json") ?? new GridType[] { })
                        .Union(new[] { GridType.FilteredBy, GridType.Manual }));
                SelectedGridType = GridTypes.GetByIdOrDefault(selectedId) ?? GridTypes.FirstOrDefault();
            }

            public void Load() {
                FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update += QuickDrive_RaceViewModel_Update;
                UpdateGridTypes();
            }

            public void Unload() {
                FilesStorage.Instance.Watcher(ContentCategory.GridTypes).Update -= QuickDrive_RaceViewModel_Update;
            }

            private void QuickDrive_RaceViewModel_Update(object sender, EventArgs e) {
                UpdateGridTypes();
            }

            [CanBeNull]
            public GridType SelectedGridType {
                get { return _selectedGridType; }
                set {
                    if (Equals(value, _selectedGridType)) return;
                    _selectedGridType = value;
                    OnPropertyChanged();
                    SaveLater();

                    UpdateOpponentsCars().Forget();
                }
            }

            private CarObject _selectedCar;

            [CanBeNull]
            public CarObject SelectedCar {
                get { return _selectedCar; }
                set {
                    if (Equals(value, _selectedCar)) return;
                    _selectedCar = value;
                    OnPropertyChanged();
                    UpdateOpponentsCars().Forget();
                }
            }

            private bool _opponentsCarsLoading;

            public bool OpponentsCarsLoading {
                get { return _opponentsCarsLoading; }
                set {
                    if (Equals(value, _opponentsCarsLoading)) return;
                    _opponentsCarsLoading = value;
                    OnPropertyChanged();
                }
            }

            private string _opponentsCarsError;

            public string OpponentsCarsError {
                get { return _opponentsCarsError; }
                set {
                    if (Equals(value, _opponentsCarsError)) return;
                    _opponentsCarsError = value;
                    OnPropertyChanged();
                }
            }

            public BetterObservableCollection<CarObject> OpponentsCars { get; }

            private string[] _opponentsCarsIds;

            public BetterListCollectionView OpponentsCarsView { get; }

            private string _opponentsCarsFilter;

            public string OpponentsCarsFilter {
                get { return _opponentsCarsFilter; }
                set {
                    value = value?.Trim();
                    if (Equals(value, _opponentsCarsFilter)) return;
                    _opponentsCarsFilter = value;
                    OnPropertyChanged();

                    using (OpponentsCarsView.DeferRefresh()) {
                        if (string.IsNullOrEmpty(value)) {
                            OpponentsCarsView.Filter = null;
                        } else {
                            var filter = Filter.Create(CarObjectTester.Instance, value);
                            OpponentsCarsView.Filter = o => o is CarObject && filter.Test((CarObject)o);
                        }
                    }

                    SaveLater();
                }
            }

            public void AddOpponentsCarsFilter() {
                if (string.IsNullOrEmpty(OpponentsCarsFilter)) return;
                OpponentsCarsFilterHistory.ReplaceEverythingBy(OpponentsCarsFilterHistory.Where(x =>
                        !string.Equals(x, OpponentsCarsFilter, StringComparison.OrdinalIgnoreCase)).Take(10).Prepend(OpponentsCarsFilter));
            }

            private const string KeyOpponentsCarsFilterHistory = "QuickDrive_Race.OpponentsCarsFilterHistory";

            private static BetterObservableCollection<string> _opponentsCarsFilterHistory;

            public BetterObservableCollection<string> OpponentsCarsFilterHistory {
                get {
                    if (_opponentsCarsFilterHistory != null) return _opponentsCarsFilterHistory;

                    _opponentsCarsFilterHistory = new BetterObservableCollection<string>(ValuesStorage.GetStringList(KeyOpponentsCarsFilterHistory));
                    _opponentsCarsFilterHistory.CollectionChanged += OpponentsCarsFilterHistory_CollectionChanged;
                    return _opponentsCarsFilterHistory;
                }
            }

            private void OpponentsCarsFilterHistory_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                ValuesStorage.Set(KeyOpponentsCarsFilterHistory, _opponentsCarsFilterHistory);
            }

            private RelayCommand _addOpponentCarCommand;

            public RelayCommand AddOpponentCarCommand => _addOpponentCarCommand ?? (_addOpponentCarCommand = new RelayCommand(o => {
                SelectedGridType = GridType.Manual;

                var dialog = new SelectCarDialog(CarsManager.Instance.GetDefault());
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

                OpponentsCars.Add(dialog.SelectedCar);
                SaveLater();
            }));

            private RelayCommand _removeOpponentCarCommand;

            public RelayCommand RemoveOpponentCarCommand => _removeOpponentCarCommand ?? (_removeOpponentCarCommand = new RelayCommand(o => {
                SelectedGridType = GridType.Manual;

                var listBox = o as ListBox;
                if (listBox == null) return;

                var selected = listBox.SelectedItems.OfType<CarObject>().ToList();
                foreach (var selectedOpponentsCar in selected) {
                    OpponentsCars.Remove(selectedOpponentsCar);
                }

                listBox.SelectedItems.Clear();
                SaveLater();
            }, o => {
                var listBox = o as ListBox;
                return listBox?.SelectedItems != null;
            }));

            private async Task UpdateOpponentsCars() {
                if (SelectedGridType == GridType.Manual) {
                    OpponentsCarsLoading = true;

                    if (!CarsManager.Instance.IsLoaded) {
                        await CarsManager.Instance.EnsureLoadedAsync();
                    }

                    if (_opponentsCarsIds != null) {
                        OpponentsCars.ReplaceEverythingBy(from id in _opponentsCarsIds
                                                          let car = CarsManager.Instance.GetById(id)
                                                          where car != null
                                                          select car);
                        _opponentsCarsIds = null;
                    }

                    OpponentsCarsLoading = false;
                    return;
                }

                _opponentsCarsIds = null;

                if (SelectedCar == null || _selectedTrack == null) {
                    OpponentsCars.Clear();
                } else if (SelectedGridType == null || SelectedGridType == GridType.SameCar) {
                    OpponentsCars.ReplaceEverythingBy(new[] { SelectedCar });
                } else {
                    OpponentsCarsLoading = true;

                    if (!CarsManager.Instance.IsLoaded) {
                        await CarsManager.Instance.EnsureLoadedAsync();
                    }

                    try {
                        OpponentsCars.ReplaceEverythingBy(await FindAppropriateCars(SelectedCar, _selectedTrack, SelectedGridType, false));
                        OpponentsCarsError = null;
                    } catch (Exception e) {
                        OpponentsCars.Clear();
                        OpponentsCarsError = "Can’t filter cars for starting grid";
                        Logging.Warning("UpdateOpponentsCars failed: " + e);
                    }

                    OpponentsCarsLoading = false;
                }
            }
            
            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                var selectedCar = CarsManager.Instance.GetById(basicProperties.CarId);
                var selectedTrack = TracksManager.Instance.GetLayoutById(basicProperties.TrackId, basicProperties.TrackConfigurationId);

                IEnumerable<Game.AiCar> botCars;

                using (var waiting = new WaitingDialog()) {
                    if (selectedCar == null || !selectedCar.Enabled) {
                        ModernDialog.ShowMessage("Please, select some non-disabled car.", "Can’t start race", MessageBoxButton.OK);
                        return;
                    }

                    if (selectedTrack == null) {
                        ModernDialog.ShowMessage("Please, select a track.", "Can’t start race", MessageBoxButton.OK);
                        return;
                    }

                    if (OpponentsNumber < 1) {
                        ModernDialog.ShowMessage("Please, set at least one opponent.", "Can’t start race", MessageBoxButton.OK);
                        return;
                    }

                    CarObject[] cars;
                    var cancellation = waiting.CancellationToken;

                    if (SelectedGridType == null || SelectedGridType == GridType.SameCar) {
                        cars = new[] { selectedCar };
                    } else {
                        if (!CarsManager.Instance.IsLoaded) {
                            waiting.Report("Grid building…");
                            await CarsManager.Instance.EnsureLoadedAsync();
                            if (cancellation.IsCancellationRequested) return;
                        }

                        try {
                            waiting.Report("AI cars filtering…");
                            cars = await FindAppropriateCars(selectedCar, selectedTrack, SelectedGridType, true, cancellation);
                            if (cancellation.IsCancellationRequested) return;

                            Logging.Write("Appropriate cars: " + cars.JoinToString(", "));
                            if (SelectedGridType.Test) return;
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t filter appropriate cars for starting grid",
                                    "If you made any changes to GridTypes.json, make sure they’re all right.", e);
                            return;
                        }

                        if (cars.Length == 0) {
                            ModernDialog.ShowMessage("Attempt to find any car fitting selected grid type is failed.", "Can’t start race", MessageBoxButton.OK);
                            return;
                        }
                    }

                    waiting.Report("AI cars loading…");
                    botCars = await GenerateAiCars(selectedCar, selectedTrack, OpponentsNumber, cars);
                    if (cancellation.IsCancellationRequested) return;
                }

                await StartAsync(new Game.StartProperties {
                    BasicProperties = new Game.BasicProperties {
                        CarId = selectedCar.Id,
                        CarSkinId = selectedCar.SelectedSkin?.Id,
                        TrackId = selectedTrack.Id,
                        TrackConfigurationId = selectedTrack.LayoutId
                    },
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = GetModeProperties(botCars)
                });
            }

            protected virtual Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.RaceProperties {
                    AiLevel = AiLevelFixed ? AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = JumpStartPenalty,
                    StartingPosition = StartingPosition == 0 ? MathUtils.Random(1, OpponentsNumber + 2) : StartingPosition,
                    RaceLaps = LapsNumber,
                    BotCars = botCars
                };
            }

            private static string PrepareScript(string script) {
                script = script.Trim();
                if (script.Contains('\n')) return script;
                
                script = $@"return function(tested)
                    return {script}
                end";
                Logging.Write(script);
                return script;
            }

            private async Task<CarObject[]> FindAppropriateCars([NotNull] CarObject car, [NotNull] TrackBaseObject track, [NotNull] GridType type, 
                    bool useUserFilter, CancellationToken cancellation = default(CancellationToken)) {
                if (car == null) throw new ArgumentNullException(nameof(car));
                if (track == null) throw new ArgumentNullException(nameof(track));
                if (type == null) throw new ArgumentNullException(nameof(type));

                if (type == GridType.FilteredBy && !useUserFilter) {
                    return CarsManager.Instance.LoadedOnly.Where(x => x.Enabled).ToArray();
                }

                await Task.Delay(200, cancellation);
                return await Task.Run(() => {
                    IEnumerable<CarObject> carsEnumerable;

                    if (type == GridType.SameGroup) {
                        var parent = car.Parent ?? car;
                        carsEnumerable = parent.Children.Prepend(parent);
                    } else if (type == GridType.Manual) {
                        carsEnumerable = OpponentsCars;
                    } else {
                        carsEnumerable = CarsManager.Instance.LoadedOnly;
                    }

                    carsEnumerable = carsEnumerable.Where(x => x.Enabled);

                    if (!string.IsNullOrWhiteSpace(type.Filter)) {
                        var filter = Filter.Create(CarObjectTester.Instance, type.Filter);
                        carsEnumerable = carsEnumerable.Where(filter.Test);
                    }

                    if (!string.IsNullOrWhiteSpace(type.Script)) {
                        var state = LuaHelper.GetExtended();
                        if (state == null) throw new Exception("Can’t initialize Lua");

                        state.Globals["selected"] = car;
                        state.Globals["track"] = track;

                        var result = state.DoString(PrepareScript(type.Script));
                        if (result.Type == DataType.Boolean && !result.Boolean) return new CarObject[0];

                        var fn = result.Function;
                        if (fn == null) throw new Exception("Invalid script");

                        carsEnumerable = carsEnumerable.Where(x => fn.Call(x).Boolean);
                    }

                    if (useUserFilter && !string.IsNullOrEmpty(OpponentsCarsFilter)) {
                        var filter = Filter.Create(CarObjectTester.Instance, OpponentsCarsFilter);
                        carsEnumerable = carsEnumerable.Where(filter.Test);
                    }

                    return carsEnumerable.ToArray();
                }, cancellation);
            }

            private async Task<IEnumerable<Game.AiCar>> GenerateAiCars(CarObject selectedCar, AcJsonObjectNew selectedTrack, int opponentsNumber, params CarObject[] opponentsCars) {
                NameNationality[] nameNationalities = null;
                var trackCountry = selectedTrack.Country == null
                        ? null : DataProvider.Instance.Countries.GetValueOrDefault(selectedTrack.Country.Trim().ToLower()) ?? "Italy";

                if (opponentsNumber == 7 && AppArguments.GetBool(AppFlag.NfsPorscheTribute)) {
                    nameNationalities = new[] {
                        new NameNationality { Name = "Dylan", Nationality = "Wales" },
                        new NameNationality { Name = "Parise", Nationality = "Italy" },
                        new NameNationality { Name = "Steele", Nationality = "United States" },
                        new NameNationality { Name = "Wingnut", Nationality = "England" },
                        new NameNationality { Name = "Leadfoot", Nationality = trackCountry },
                        new NameNationality { Name = "Amazon", Nationality = trackCountry },
                        new NameNationality { Name = "Backlash", Nationality = "United States" }
                    };
                } else if (DataProvider.Instance.NationalitiesAndNames.Any()) {
                    nameNationalities = GoodShuffle.Get(DataProvider.Instance.NationalitiesAndNamesList).Take(opponentsNumber).ToArray();
                }

                foreach (var car in opponentsCars) {
                    await car.SkinsManager.EnsureLoadedAsync();
                }

                var opponentsCarsEntries = (from x in opponentsCars
                                            select new {
                                                Car = x,
                                                Skins = GoodShuffle.Get(x.SkinsManager.LoadedOnlyCollection)
                                            }).ToList();
                var opponentsCarsShuffled = GoodShuffle.Get(opponentsCarsEntries);

                var playerCarEntry = opponentsCarsEntries.FirstOrDefault(x => x.Car == selectedCar);
                if (playerCarEntry != null) {
                    opponentsCarsShuffled.IgnoreOnce(playerCarEntry);
                    playerCarEntry.Skins.IgnoreOnce(selectedCar.SelectedSkin);
                }

                var aiLevels = from i in Enumerable.Range(0, opponentsNumber)
                               select AiLevelMin + (int)((opponentsNumber < 2 ? 1f : (float)i / (opponentsNumber - 1)) * (AiLevel - AiLevelMin));
                if (AiLevelArrangeRandomly) {
                    aiLevels = GoodShuffle.Get(aiLevels);
                } else if (!AiLevelArrangeReverse) {
                    aiLevels = aiLevels.Reverse();
                }

                var list = aiLevels.Take(opponentsNumber).ToList();
                return from i in Enumerable.Range(0, opponentsNumber)
                       let entry = opponentsCarsShuffled.Next
                       select new Game.AiCar {
                           AiLevel = AiLevelFixed ? 100 : list[i],
                           CarId = entry.Car.Id,
                           DriverName = nameNationalities?[i].Name ?? "AI #" + i,
                           Nationality = nameNationalities?[i].Nationality ?? trackCountry,
                           Setup = "",
                           SkinId = entry.Skins.Next?.Id
                       };
            }

            [CanBeNull]
            private TrackBaseObject _selectedTrack;
            private GridType _selectedGridType;

            public override void OnSelectedUpdated(CarObject selectedCar, TrackBaseObject selectedTrack) {
                SelectedCar = selectedCar;

                if (_selectedTrack != null) {
                    _selectedTrack.PropertyChanged -= SelectedTrack_PropertyChanged;
                }

                _selectedTrack = selectedTrack;
                if (_selectedTrack == null) return;

                TrackPitsNumber = FlexibleParser.ParseInt(_selectedTrack.SpecsPitboxes, 2);
                _selectedTrack.PropertyChanged += SelectedTrack_PropertyChanged;
            }

            void SelectedTrack_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (_selectedTrack != null && e.PropertyName == nameof(_selectedTrack.SpecsPitboxes)) {
                    TrackPitsNumber = FlexibleParser.ParseInt(_selectedTrack.SpecsPitboxes, 2);
                }
            }

            int IComparer.Compare(object x, object y) {
                return (x as AcObjectNew)?.CompareTo(y as AcObjectNew) ?? 0;
            }
        }

        private void OpponentsCarsFilterTextBox_OnLostFocus(object sender, RoutedEventArgs e) {
            ((ViewModel)Model).AddOpponentsCarsFilter();
        }
    }
}
