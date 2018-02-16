using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class KunosCareerObject : AcIniObject, IComparer {
        public KunosCareerObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            ChampionshipDrivers = new BetterObservableCollection<ChampionshipDriverEntry>();
            ChampionshipDriversView = new BetterListCollectionView(ChampionshipDrivers) { CustomSort = this };
        }

        public class ChampionshipDriverEntry : NotifyPropertyChanged, IWithId<int> {
            private int _points;
            private int _takenPlace;

            public bool IsPlayer { get; set; }
            public bool TakenPlacePrizePlace { get; } = true;
            public string Name { get; set; }
            public string Nationality { get; set; }
            public string SetupId { get; set; }
            public int AiLevel { get; set; }
            public CarObject Car { get; set; }
            public CarSkinObject CarSkin { get; set; }

            public int Points {
                get => _points;
                set => Apply(value, ref _points);
            }

            public int TakenPlace {
                get => _takenPlace;
                set => Apply(value, ref _takenPlace);
            }

            public int Id { get; set; }
        }

        public int Compare(object x, object y) {
            return x is ChampionshipDriverEntry xc && y is ChampionshipDriverEntry yc ? yc.Points - xc.Points : 0;
        }

        private BetterObservableCollection<ChampionshipDriverEntry> _championshipDrivers;

        public BetterObservableCollection<ChampionshipDriverEntry> ChampionshipDrivers {
            get => _championshipDrivers;
            set => Apply(value, ref _championshipDrivers);
        }

        protected override void OnAcObjectOutdated() {
            foreach (var obj in Events) {
                obj.Outdate();
            }

            base.OnAcObjectOutdated();
        }

        public BetterListCollectionView ChampionshipDriversView { get; }

        private void InitializeEventsManager() {
            if (EventsManager != null) return;
            EventsManager = new KunosCareerEventsManager(Id, Type, new AcDirectories(Location, null)) {
                ScanWrapper = this
            };
            EventsManager.EventHasErrorChanged += EventsManager_EventHasErrorChanged;
            EventsManager.WrappersList.CollectionReady += Events_CollectionReady;
            EventsManager.Scan();
        }

        public class AcEventError : AcErrorWrapper {
            private readonly KunosCareerEventObject _ev;
            private readonly IAcError _baseError;

            public AcEventError(KunosCareerEventObject ev, IAcError baseError)
                    : base(baseError) {
                _ev = ev;
                _baseError = baseError;
            }

            private ICommand _startErrorFixerCommand;

            public override ICommand StartErrorFixerCommand => _startErrorFixerCommand ?? (_startErrorFixerCommand = new DelegateCommand(() => {
                _baseError.StartErrorFixerCommand.Execute(_ev);
            }, () => _baseError.StartErrorFixerCommand.CanExecute(_ev)));
        }

        private void EventsManager_EventHasErrorChanged(object sender, EventArgs e) {
            foreach (var er in Errors.OfType<AcEventError>().ToList()) {
                RemoveError(er);
            }

            foreach (var er in Events.Append((KunosCareerEventObject)sender)
                    .SelectMany(x => x.InnerErrors.Select(y => new AcEventError(x, y)))) {
                AddError(er);
            }
        }

        private void LoadOpponents() {
            if (Type != KunosCareerObjectType.Championship) return;

            InitializeEventsManager();
            var firstEvent = GetFirstEventOrNull();
            if (firstEvent == null) {
                AddError(AcErrorType.Data_KunosCareerEventsAreMissing, DisplayName);
            }

            var ini = new IniFile(OpponentsIniFilename);
            var drivers = LinqExtension.RangeFrom(1).Select(x => $"AI{x}").TakeWhile(ini.ContainsKey).Select(x => ini[x]).Select((section, id) => {
                var model = section.GetNonEmpty("MODEL");
                if (model == null) {
                    Logging.Error($"Section AI{id + 1}: MODEL is required, fallback to default");
                    model = CarsManager.Instance.GetDefault()?.Id ?? "";
                }

                var skin = section.GetNonEmpty("SKIN");
                if (skin == null) {
                    Logging.Error($"Section AI{id + 1}: SKIN is required, fallback to default");
                }

                var car = CarsManager.Instance.GetById(model);
                CarSkinObject carSkin;
                if (car == null) {
                    AddError(AcErrorType.Data_KunosCareerCarIsMissing, section.GetNonEmpty("MODEL"));
                    carSkin = null;
                } else {
                    carSkin = skin == null ? car.GetFirstSkinOrNull() : car.GetSkinByIdFromConfig(skin);
                    if (carSkin == null) {
                        if (!KunosEventObjectBase.OptionIgnoreMissingSkins) {
                            AddError(AcErrorType.Data_KunosCareerCarSkinIsMissing, car.DisplayName, section.GetNonEmpty("SKIN"));
                        }

                        carSkin = (CarSkinObject)car.SkinsManager.WrappersList.RandomElementOrDefault()?.Loaded();
                    }
                }

                return new ChampionshipDriverEntry {
                    Id = id,
                    Name = section.GetPossiblyEmpty("DRIVER_NAME"),
                    Nationality = section.GetPossiblyEmpty("NATIONALITY"),
                    AiLevel = section.GetInt("AI_LEVEL", 100),
                    SetupId = section.GetPossiblyEmpty("SETUP"),
                    Car = car,
                    CarSkin = carSkin
                };
            }).Prepend(new ChampionshipDriverEntry {
                Id = -1,
                Name = SettingsHolder.Drive.PlayerName,
                Nationality = SettingsHolder.Drive.PlayerNationality,
                Car = firstEvent?.CarObject,
                CarSkin = firstEvent?.CarSkin,
                IsPlayer = true
            });
            ChampionshipDrivers.ReplaceEverythingBy(drivers);
        }

        private void Events_CollectionReady(object sender, EventArgs e) {
            if (Type != KunosCareerObjectType.Championship) return;

            var points = Events.Sum(x => ChampionshipPointsPerPlace.ArrayElementAtOrDefault(x.TakenPlace - 1));
            if (points == ChampionshipPoints) return;

            Logging.Write($"Summary points restored: {points} (was {ChampionshipPoints})");
            ChampionshipPoints = points;
            ChampionshipDrivers.GetById(-1).Points = ChampionshipPoints;
            ChampionshipDriversView.Refresh();
        }

        public string DisplayType => Type.GetDescription();

        public bool IsStarted => CompletedEvents > 0;

        public string DisplayGo => CompletedEvents > 0 ? ToolsStrings.KunosCareer_Resume : ToolsStrings.KunosCareer_Start;

        public string DisplayRequired => (RequiredAnySeries && RequiredSeries.Length > 1 ? @"Any of " : "") +
                RequiredSeries.Select(x => KunosCareerManager.Instance.GetById(x)?.Code ?? $"<{x}>").JoinToString(@", ");

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "series.ini");
            OpponentsIniFilename = Path.Combine(Location, "opponents.ini");
            PreviewImage = Path.Combine(Location, "preview.png");
            StartImage = Path.Combine(Location, "start.png");
            StartVideo = Path.Combine(Location, "start.ogv");
        }

        public string OpponentsIniFilename { get; private set; }

        public string PreviewImage { get; private set; }

        public string StartImage { get; private set; }

        public string StartVideo { get; private set; }

        #region From main ini file
        private string _description;

        public string Description {
            get => _description;
            set => Apply(value, ref _description);
        }

        private string _code;

        public string Code {
            get => _code;
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    value = null;
                }

                if (Equals(value, _code)) return;
                _code = value;
                OnPropertyChanged();
            }
        }

        private KunosCareerObjectType _type;

        public KunosCareerObjectType Type {
            get => _type;
            set {
                if (Equals(value, _type)) return;
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayType));
                _championshipResetCommand?.RaiseCanExecuteChanged();
            }
        }

        private string[] _requiredSeries;

        public string[] RequiredSeries {
            get => _requiredSeries;
            set => Apply(value, ref _requiredSeries);
        }

        private bool _requiredAnySeries;

        public bool RequiredAnySeries {
            get => _requiredAnySeries;
            set => Apply(value, ref _requiredAnySeries);
        }

        private int[] _pointsForPlace;

        public int[] PointsForPlace {
            get => _pointsForPlace;
            set => Apply(value, ref _pointsForPlace);
        }

        private int _championshipPointsGoal;

        public int ChampionshipPointsGoal {
            get => _championshipPointsGoal;
            set => Apply(value, ref _championshipPointsGoal);
        }

        private int _championshipRankingGoal;

        public int ChampionshipRankingGoal {
            get => _championshipRankingGoal;
            set => Apply(value, ref _championshipRankingGoal);
        }

        public string DisplayChampionshipGoal
            => ChampionshipPointsGoal == 0 ? $"[b]{ChampionshipRankingGoal.ToOrdinalShort(ToolsStrings.KunosCareer_Place)}[/b] {ToolsStrings.KunosCareer_Place}"
                    : $"[b]{ChampionshipPointsGoal}[/b] {PluralizingConverter.Pluralize(ChampionshipPointsGoal, ToolsStrings.KunosCareer_Point)}";

        private int _firstPlacesGoal;

        public int FirstPlacesGoal {
            get => _firstPlacesGoal;
            set => Apply(value, ref _firstPlacesGoal);
        }

        private int _secondPlacesGoal;

        public int SecondPlacesGoal {
            get => _secondPlacesGoal;
            set => Apply(value, ref _secondPlacesGoal);
        }

        private int _thirdPlacesGoal;

        public int ThirdPlacesGoal {
            get => _thirdPlacesGoal;
            set => Apply(value, ref _thirdPlacesGoal);
        }

        private int[] _championshipPointsPerPlace;

        public int[] ChampionshipPointsPerPlace {
            get => _championshipPointsPerPlace;
            set => Apply(value, ref _championshipPointsPerPlace);
        }

        private KunosCareerObject _nextCareerObject;

        public KunosCareerObject NextCareerObject {
            get => _nextCareerObject;
            set => Apply(value, ref _nextCareerObject);
        }
        #endregion

        #region Progress values
        private bool _isAvailable;

        public bool IsAvailable {
            get => _isAvailable;
            set => Apply(value, ref _isAvailable);
        }

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

        private int _firstPlaces;

        public int FirstPlaces {
            get => _firstPlaces;
            set => Apply(value, ref _firstPlaces);
        }

        private int _secondPlaces;

        public int SecondPlaces {
            get => _secondPlaces;
            set => Apply(value, ref _secondPlaces);
        }

        private int _thirdPlaces;

        public int ThirdPlaces {
            get => _thirdPlaces;
            set => Apply(value, ref _thirdPlaces);
        }

        private int _firstPlacesNeeded;

        public int FirstPlacesNeeded {
            get => _firstPlacesNeeded;
            set => Apply(value, ref _firstPlacesNeeded);
        }

        private int _secondPlacesNeeded;

        public int SecondPlacesNeeded {
            get => _secondPlacesNeeded;
            set => Apply(value, ref _secondPlacesNeeded);
        }

        private int _thirdPlacesNeeded;

        public int ThirdPlacesNeeded {
            get => _thirdPlacesNeeded;
            set => Apply(value, ref _thirdPlacesNeeded);
        }

        private int _championshipPointsNeeded;

        public int ChampionshipPointsNeeded {
            get => _championshipPointsNeeded;
            set => Apply(value, ref _championshipPointsNeeded);
        }

        private bool _isCompleted;

        public bool IsCompleted {
            get => _isCompleted;
            set => Apply(value, ref _isCompleted);
        }

        private long _lastSelectedTimestamp;

        public long LastSelectedTimestamp {
            get => _lastSelectedTimestamp;
            set {
                if (Equals(value, _lastSelectedTimestamp)) return;
                _lastSelectedTimestamp = value;
                OnPropertyChanged();
                SaveProgress(false);
            }
        }

        private int _championshipPoints = -1;

        public int ChampionshipPoints {
            get => _championshipPoints;
            set {
                if (Equals(value, _championshipPoints)) return;
                _championshipPoints = value;
                OnPropertyChanged();

                if (ChampionshipAiPoints != null) {
                    ChampionshipPlace = ChampionshipAiPoints.Count(x => x > ChampionshipPoints) + 1;
                }
            }
        }

        private IReadOnlyList<int> _championshipAiPoints;

        [CanBeNull]
        public IReadOnlyList<int> ChampionshipAiPoints {
            get => _championshipAiPoints;
            set {
                if (Equals(value, _championshipAiPoints)) return;
                _championshipAiPoints = value;
                OnPropertyChanged();

                if (ChampionshipPoints != -1) {
                    ChampionshipPlace = (ChampionshipAiPoints?.Count(x => x > ChampionshipPoints) ?? 0) + 1;
                }
            }
        }

        private int _championshipPlace;

        public int ChampionshipPlace {
            get => _championshipPlace;
            set => Apply(value, ref _championshipPlace);
        }
        #endregion

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            InitializeEventsManager();
            UpdateIsCompletedFlag();
            LoadProgress();
        }

        protected override void LoadData(IniFile ini) {
            Name = ini["SERIES"].GetPossiblyEmpty("NAME");
            Code = ini["SERIES"].GetPossiblyEmpty("CODE");
            Description = AcStringValues.DecodeDescription(ini["SERIES"].GetPossiblyEmpty("DESCRIPTION"));

            PointsForPlace = ini["SERIES"].GetPossiblyEmpty("POINTS")?.Split(',').Select(x => FlexibleParser.TryParseInt(x)).OfType<int>().ToArray();
            ChampionshipPointsGoal = ini["GOALS"].GetIntNullable("POINTS") ?? 0;
            ChampionshipRankingGoal = ini["GOALS"].GetIntNullable("RANKING") ?? 0;
            ThirdPlacesGoal = ini["GOALS"].GetIntNullable("TIER1") ?? 0;
            SecondPlacesGoal = ini["GOALS"].GetIntNullable("TIER2") ?? 0;
            FirstPlacesGoal = ini["GOALS"].GetIntNullable("TIER3") ?? 0;
            Type = ChampionshipPointsGoal == 0 && ChampionshipRankingGoal == 0 || PointsForPlace?.Sum() == 0
                    ? KunosCareerObjectType.SingleEvents : KunosCareerObjectType.Championship;

            RequiredSeries = ini["SERIES"].GetStrings("REQUIRES").ToArray();
            RequiredAnySeries = ini["SERIES"].GetBool("REQUIRESANY", false);

            ChampionshipPointsPerPlace = ini["SERIES"].GetStrings("POINTS").Select(x => FlexibleParser.TryParseInt(x)).OfType<int>().ToArray();

            if (Type == KunosCareerObjectType.Championship) {
                LoadOpponents();
            }
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) return true;
            if (FileUtils.Affects(filename, OpponentsIniFilename) && Type == KunosCareerObjectType.Championship) {
                LoadOpponents();
            }

            return true;
        }

        public override void SaveData(IniFile ini) {
            ini["SERIES"].Set("NAME", Name);
            ini["SERIES"].Set("CODE", Code);
            ini["SERIES"].Set("DESCRIPTION", AcStringValues.EncodeDescription(Description));
            ini["SERIES"].Set("POINTS", PointsForPlace.JoinToString(@","));
            ini["GOALS"].Set("POINTS", ChampionshipPointsGoal);
            ini["GOALS"].Set("RANKING", ChampionshipRankingGoal);
            ini["GOALS"].Set("TIER1", ThirdPlacesGoal);
            ini["GOALS"].Set("TIER2", SecondPlacesGoal);
            ini["GOALS"].Set("TIER3", FirstPlacesGoal);
            ini["SERIES"].Set("REQUIRES", RequiredSeries);
            ini["SERIES"].Set("REQUIRESANY", RequiredAnySeries);
        }

        internal void UpdateIsCompletedFlag() {
            IsCompleted = KunosCareerProgress.Instance.Completed.ArrayContains(Id);
        }

        public void UpdateIfIsAvailable() {
            bool Fn(string x) => KunosCareerProgress.Instance.Completed.ArrayContains(x);
            IsAvailable = RequiredSeries.Length == 0 || (RequiredAnySeries ? RequiredSeries.Any(Fn) : RequiredSeries.All(Fn));
        }

        private void LoadProgressFromEntry() {
            UpdateIfIsAvailable();

            foreach (var driver in ChampionshipDrivers) {
                driver.Points = 0;
            }

            var entry = KunosCareerProgress.Instance.Entries.GetValueOrDefault(Id);
            if (entry == null) {
                CompletedEvents = 0;

                FirstPlaces = 0;
                SecondPlaces = 0;
                ThirdPlaces = 0;
                ChampionshipAiPoints = new int[0];
                ChampionshipPoints = 0;

                _lastSelectedTimestamp = 0;
            } else {
                var count = EventsWrappers.Count;
                CompletedEvents = entry.EventsResults.Where(x => x.Key < count).Count(x => x.Value > 0);

                if (Type == KunosCareerObjectType.SingleEvents) {
                    FirstPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 3);
                    SecondPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 2);
                    ThirdPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 1);
                    ChampionshipAiPoints = new int[0];
                    ChampionshipPoints = 0;
                } else {
                    FirstPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 1);
                    SecondPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 2);
                    ThirdPlaces = entry.EventsResults.Count(x => x.Key < count && x.Value == 3);
                    ChampionshipAiPoints = ChampionshipDrivers.Select((x, i) => entry.AiPoints.GetValueOrDefault(i)).ToList();
                    ChampionshipPoints = entry.Points ?? 0;

                    for (var i = 0; i < ChampionshipAiPoints.Count; i++) {
                        var driverEntry = ChampionshipDrivers.GetByIdOrDefault(i);
                        if (driverEntry != null) {
                            driverEntry.Points = ChampionshipAiPoints[i];
                        } else {
                            Logging.Warning("Missing driver entry with ID=" + i);
                        }
                    }

                    var place = 0;
                    foreach (var driver in ChampionshipDrivers.OrderBy(x => x.Points)) {
                        driver.TakenPlace = place++;
                    }

                    ChampionshipDrivers.GetById(-1).Points = ChampionshipPoints;
                }

                _lastSelectedTimestamp = entry.LastSelectedTimestamp;

                if (EventsManager?.IsScanned == true) {
                    _selectedEvent = EventsManager.GetByNumber(entry.SelectedEvent) ?? _selectedEvent;
                    OnPropertyChanged(nameof(SelectedEvent));
                }
            }

            ChampionshipDriversView.Refresh();
            OnPropertyChanged(nameof(LastSelectedTimestamp));

            if (EventsManager != null) {
                foreach (var eventObject in EventsManager.Loaded) {
                    eventObject.LoadProgress();
                }
            }
        }

        private void RecalculateProgressValues() {
            var firstPlacesNeeded = FirstPlacesGoal - FirstPlaces;
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
            ThirdPlacesNeeded = thirdPlacesNeeded;

            ChampionshipPointsNeeded = ChampionshipPointsGoal - ChampionshipPoints;
        }

        public void LoadProgress() {
            LoadProgressFromEntry();
            RecalculateProgressValues();
        }

        public void SaveProgress(bool globalUpdate) {
            switch (Type) {
                case KunosCareerObjectType.Championship:
                    var events = Events.ToList();
                    KunosCareerProgress.Instance.UpdateEntry(Id,
                            new KunosCareerProgressEntry(
                                    events.FirstOrDefault(x => x.IsAvailable && !x.IsPassed)?.EventNumber ?? events.LastOrDefault()?.EventNumber ?? 0,
                                    events.Select((x, i) => new {
                                        Key = i,
                                        Place = x.TakenPlace
                                    }).TakeWhile(x => x.Place != 0).ToDictionary(x => x.Key, x => x.Place),
                                    ChampionshipPoints,
                                    ChampionshipAiPoints?.Select((x, i) => new {
                                        Key = i,
                                        Points = x
                                    }).ToDictionary(x => x.Key, x => x.Points)),
                            globalUpdate);
                    break;
                case KunosCareerObjectType.SingleEvents:
                    KunosCareerProgress.Instance.UpdateEntry(Id,
                            new KunosCareerProgressEntry(
                                    SelectedEvent?.EventNumber ?? 0,
                                    Events.Select((x, i) => new {
                                        Key = i,
                                        Place = x.TakenPlace == PlaceConditions.UnremarkablePlace ? 0 : 4 - x.TakenPlace
                                    }).Where(x => x.Place != 0).ToDictionary(x => x.Key, x => x.Place),
                                    null,
                                    null),
                            globalUpdate);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override int CompareTo(AcPlaceholderNew o) {
            var c = o as KunosCareerObject;
            return c == null ? base.CompareTo(o) : AlphanumComparatorFast.Compare(Id, c.Id);
        }

        private DelegateCommand _championshipResetCommand;

        public DelegateCommand ChampionshipResetCommand => _championshipResetCommand ?? (_championshipResetCommand = new DelegateCommand(() => {
            KunosCareerProgress.Instance.UpdateEntry(Id, new KunosCareerProgressEntry(0, null, 0, null), true);
            KunosCareerProgress.Instance.Completed = KunosCareerProgress.Instance.Completed.ApartFrom(Id).ToArray();
        }, () => Type == KunosCareerObjectType.Championship && IsStarted));
    }
}
