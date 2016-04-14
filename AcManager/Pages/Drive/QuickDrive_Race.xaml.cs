using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using Newtonsoft.Json;
using NLua;
using StringBasedFilter;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Race : IQuickDriveModeControl {
        public QuickDrive_Race() {
            InitializeComponent();
            DataContext = new QuickDrive_RaceViewModel();
        }

        public QuickDriveModeViewModel Model => (QuickDrive_RaceViewModel)DataContext;

        public QuickDrive_RaceViewModel ActualModel => (QuickDrive_RaceViewModel)DataContext;

        public class QuickDrive_RaceViewModel : QuickDriveModeViewModel, IComparer {
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

            private int _lapsNumber;

            public int LapsNumber {
                get { return _lapsNumber; }
                set {
                    if (Equals(value, _lapsNumber)) return;
                    _lapsNumber = MathUtils.Clamp(value, 1, 40);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LapsNumberString));
                    SaveLater();
                }
            }

            public string LapsNumberString => LapsNumber + LocalizationHelper.MultiplyForm(LapsNumber, @" lap", @" laps");

            private int _opponentsNumber;
            private int _unclampedOpponentsNumber;

            public int OpponentsNumber {
                get { return _opponentsNumber; }
                set {
                    if (Equals(value, _opponentsNumber)) return;

                    _unclampedOpponentsNumber = value;
                    _opponentsNumber = MathUtils.Clamp(value, 1, OpponentsNumberLimit);

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpponentsNumberString));
                    OnPropertyChanged(nameof(StartingPositionLimit));

                    if (_last || StartingPosition > StartingPositionLimit) {
                        StartingPosition = StartingPositionLimit;
                    } else if (StartingPosition == StartingPositionLimit) {
                        _last = true;
                        OnPropertyChanged(nameof(DisplayStartingPosition));
                    }

                    SaveLater();
                }
            }

            public string OpponentsNumberString => OpponentsNumber + LocalizationHelper.MultiplyForm(OpponentsNumber, @" opponent", @" opponents");

            private int _aiLevel;

            public int AiLevel {
                get { return _aiLevel; }
                set {
                    if (Equals(value, _aiLevel)) return;
                    _aiLevel = MathUtils.Clamp(value, 75, 100);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _last;
            private int _startingPosition;

            public int StartingPosition {
                get { return _startingPosition; }
                set {
                    if (Equals(value, _startingPosition)) return;
                    _startingPosition = value;

                    _last = value == StartingPositionLimit;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayStartingPosition));
                    SaveLater();
                }
            }

            public string DisplayStartingPosition => StartingPosition == 0 ? @"Random" : _last ? @"Last" : LocalizationHelper.GetOrdinalReadable(StartingPosition);

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

            private class SaveableData {
                public bool? Penalties;
                public int? AiLevel, LapsNumber, OpponentsNumber, StartingPosition;
                public string GridTypeId, OpponentsCarsFilter;
                public string[] ManualList;
            }

            public QuickDrive_RaceViewModel(bool initialize = true) {
                OpponentsCars = new BetterObservableCollection<CarObject>();
                OpponentsCarsView = new BetterListCollectionView(OpponentsCars) { CustomSort = this };

                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Race", () => new SaveableData {
                    Penalties = Penalties,
                    AiLevel = AiLevel,
                    LapsNumber = LapsNumber,
                    OpponentsNumber = OpponentsNumber,
                    StartingPosition = StartingPosition,
                    GridTypeId = SelectedGridType?.Id,
                    OpponentsCarsFilter = OpponentsCarsFilter,
                    ManualList = _opponentsCarsIds ?? (SelectedGridType == GridType.Manual ? OpponentsCars.Select(x => x.Id).ToArray() : null)
                }, o => {
                    Penalties = o.Penalties ?? true;
                    AiLevel = o.AiLevel ?? 92;
                    LapsNumber = o.LapsNumber ?? 2;
                    OpponentsNumber = o.OpponentsNumber ?? 3;
                    StartingPosition = o.StartingPosition ?? 4;

                    UpdateGridTypes();
                    SelectedGridType = GridTypes.GetByIdOrDefault(o.GridTypeId) ?? GridType.SameCar;

                    _opponentsCarsIds = SelectedGridType == GridType.Manual ? o.ManualList : null;
                    OpponentsCarsFilter = o.OpponentsCarsFilter;
                }, () => {
                    Penalties = true;
                    AiLevel = 92;
                    LapsNumber = 2;
                    OpponentsNumber = 3;
                    StartingPosition = 4;
                    SelectedGridType = GridType.SameCar;
                    OpponentsCarsFilter = string.Empty;
                });

                if (initialize) {
                    Saveable.Init();
                } else {
                    Saveable.Reset();
                }
            }

            public class GridType : IWithId {
                public static readonly GridType SameCar = new GridType("Same car");
                public static readonly GridType SameGroup = new GridType("Same group");
                public static readonly GridType FilteredBy = new GridType("Filtered by…");
                public static readonly GridType Manual = new GridType("Manual");

                [JsonProperty(PropertyName = "id")]
                private string _id;

                public string Id => _id ?? (_id = AcStringValues.IdFromName(DisplayName));

                [JsonProperty(PropertyName = "name")]
                private readonly string _displayName;

                public string DisplayName => _displayName;

                [JsonProperty(PropertyName = "filter")]
                private readonly string _filter;

                public string Filter => _filter;

                [JsonProperty(PropertyName = "script")]
                private readonly string _script;

                public string Script => _script;

                [JsonProperty(PropertyName = "test")]
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

                if (SelectedGridType == null || SelectedGridType == GridType.SameCar) {
                    OpponentsCars.ReplaceEverythingBy(new[] { SelectedCar }.NonNull());
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
                        OpponentsCarsError = "Can't filter cars for starting grid";
                        Logging.Warning("UpdateOpponentsCars failed: " + e);
                    }

                    OpponentsCarsLoading = false;
                }
            }

            public override async Task Drive(CarObject selectedCar, TrackBaseObject selectedTrack, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                using (var waiting = new WaitingDialog()) {
                    await Drive(selectedCar, selectedTrack, assistsProperties, conditionProperties, trackProperties, waiting);
                }
            }

            public async Task Drive(CarObject selectedCar, TrackBaseObject selectedTrack, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties, IProgress<string> progress) {
                if (selectedCar == null || !selectedCar.Enabled) {
                    ModernDialog.ShowMessage("Please, select some non-disabled car.", "Can't start race", MessageBoxButton.OK);
                    return;
                }

                if (selectedTrack == null) {
                    ModernDialog.ShowMessage("Please, select a track.", "Can't start race", MessageBoxButton.OK);
                    return;
                }

                if (OpponentsNumber < 1) {
                    ModernDialog.ShowMessage("Please, set at least one opponent.", "Can't start race", MessageBoxButton.OK);
                    return;
                }

                CarObject[] cars;

                if (SelectedGridType == null || SelectedGridType == GridType.SameCar) {
                    cars = new[] { selectedCar };
                } else {
                    if (!CarsManager.Instance.IsLoaded) {
                        progress.Report("Grid building…");
                        await CarsManager.Instance.EnsureLoadedAsync();
                    }

                    try {
                        progress.Report("AI cars filtering…");
                        cars = await FindAppropriateCars(selectedCar, selectedTrack, SelectedGridType, true);
                        Logging.Write("Appropriate cars: " + cars.JoinToString(", "));
                        if (SelectedGridType.Test) {
                            return;
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify("Can't filter appropriate cars for starting grid",
                                "If you made any changes to GridTypes.json, make sure they're all right.", e);
                        return;
                    }

                    if (cars.Length == 0) {
                        ModernDialog.ShowMessage("Attempt to find any car fitting selected grid type is failed.", "Can't start race", MessageBoxButton.OK);
                        return;
                    }
                }

                progress.Report("AI cars loading…");
                var botCars = await GenerateAiCars(selectedCar, selectedTrack, OpponentsNumber, cars);

                progress.Report(null);
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
                    ModeProperties = new Game.RaceProperties {
                        AiLevel = AiLevel,
                        Penalties = Penalties,
                        StartingPosition = StartingPosition,
                        RaceLaps = LapsNumber,
                        BotCars = botCars
                    }
                });
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

            private async Task<CarObject[]> FindAppropriateCars(CarObject car, TrackBaseObject track, GridType type, bool useUserFilter) {
                if (type == GridType.FilteredBy && !useUserFilter) {
                    return CarsManager.Instance.LoadedOnly.Where(x => x.Enabled).ToArray();
                }

                await Task.Delay(200);
                return await Task.Run(() => {
                    IEnumerable<CarObject> carsEnumerable;

                    if (type == GridType.SameGroup) {
                        var parent = car.Parent ?? car;
                        carsEnumerable = new[] { parent }.Union(parent.Children);
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
                        if (state == null) throw new Exception("Can't initialize Lua");

                        state["selected"] = car;
                        state["track"] = track;

                        var result = state.DoString(PrepareScript(type.Script)).FirstOrDefault();
                        if (result as bool? == false) return new CarObject[0];

                        var fn = result as LuaFunction;
                        if (fn == null) throw new Exception("Invalid script");
                        carsEnumerable = carsEnumerable.Where(x => fn.Call(x).FirstOrDefault() as bool? == true);
                    }

                    if (useUserFilter && !string.IsNullOrEmpty(OpponentsCarsFilter)) {
                        var filter = Filter.Create(CarObjectTester.Instance, OpponentsCarsFilter);
                        carsEnumerable = carsEnumerable.Where(filter.Test);
                    }

                    return carsEnumerable.ToArray();
                });
            }

            private async Task<IEnumerable<Game.AiCar>> GenerateAiCars(CarObject selectedCar, AcJsonObjectNew selectedTrack, int opponentsNumber, params CarObject[] opponentsCars) {
                NameNationality[] nameNationalities = null;
                var trackCountry = selectedTrack.Country == null
                        ? null : DataProvider.Instance.Countries.GetValueOrDefault(selectedTrack.Country.Trim().ToLower()) ?? "Italy";

                if (opponentsNumber == 7) {
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

                return from i in Enumerable.Range(0, opponentsNumber)
                       let entry = opponentsCarsShuffled.Next
                       select new Game.AiCar {
                           AiLevel = 100,
                           CarId = entry.Car.Id,
                           DriverName = nameNationalities?[i].Name ?? "AI #" + i,
                           Nationality = nameNationalities?[i].Nationality ?? trackCountry,
                           Setup = "",
                           SkinId = entry.Skins.Next?.Id
                       };
            }

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
                if (e.PropertyName == "SpecsPitboxes") {
                    TrackPitsNumber = FlexibleParser.ParseInt(_selectedTrack.SpecsPitboxes, 2);
                }
            }

            int IComparer.Compare(object x, object y) {
                return (x as AcObjectNew)?.CompareTo(y as AcObjectNew) ?? 0;
            }
        }

        private void QuickDrive_Race_OnLoaded(object sender, RoutedEventArgs e) {
            ActualModel.Load();
        }

        private void QuickDrive_Race_OnUnloaded(object sender, RoutedEventArgs e) {
            ActualModel.Unload();
        }
    }
}
