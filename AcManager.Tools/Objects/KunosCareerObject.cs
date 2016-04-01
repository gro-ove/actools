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
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

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
                get { return _points; }
                set {
                    if (value == _points) return;
                    _points = value;
                    OnPropertyChanged();
                }
            }

            public int TakenPlace {
                get { return _takenPlace; }
                set {
                    if (value == _takenPlace) return;
                    _takenPlace = value;
                    OnPropertyChanged();
                }
            }

            public int Id { get; set; }
        }

        public int Compare(object x, object y) {
            var xc = x as ChampionshipDriverEntry;
            var yc = y as ChampionshipDriverEntry;
            if (xc == null || yc == null) return 0;
            return yc.Points - xc.Points;
        }

        private BetterObservableCollection<ChampionshipDriverEntry> _championshipDrivers;

        public BetterObservableCollection<ChampionshipDriverEntry> ChampionshipDrivers {
            get { return _championshipDrivers; }
            set {
                if (Equals(value, _championshipDrivers)) return;
                _championshipDrivers = value;
                OnPropertyChanged();
            }
        }

        protected override void OnAcObjectOutdated() {
            foreach (var obj in Events) {
                obj.Outdate();
            }

            base.OnAcObjectOutdated();
        }

        public BetterListCollectionView ChampionshipDriversView { get; }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            InitializeEventsManager();
        }

        private void InitializeEventsManager() {
            if (EventsManager != null) return;
            EventsManager = new KunosCareerEventsManager(Id, Type, new AcObjectTypeDirectories(Location, null)) {
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

            private RelayCommand _startErrorFixerCommand;

            public override ICommand StartErrorFixerCommand => _startErrorFixerCommand ?? (_startErrorFixerCommand = new RelayCommand(o => {
                _baseError.StartErrorFixerCommand.Execute(_ev);
            }, o => _baseError.StartErrorFixerCommand.CanExecute(_ev)));
        }

        private void EventsManager_EventHasErrorChanged(object sender, EventArgs e) {
            foreach (var er in Errors.OfType<AcEventError>().ToList()) {
                RemoveError(er);
            }

            foreach (var er in Events.Append((KunosCareerEventObject)sender)
                    .SelectMany(x => x.Errors.Select(y => new AcEventError(x, y)))) {
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
                var car = CarsManager.Instance.GetById(section.Get("MODEL"));
                CarSkinObject carSkin;
                if (car == null) {
                    AddError(AcErrorType.Data_KunosCareerCarIsMissing, section.Get("MODEL"));
                    carSkin = null;
                } else {
                    carSkin = car.GetSkinByIdFromConfig(section.Get("SKIN"));
                    if (carSkin == null) {
                        AddError(AcErrorType.Data_KunosCareerCarSkinIsMissing, car.DisplayName, section.Get("SKIN"));
                        carSkin = car.GetFirstSkinOrNull();
                    }
                }

                return new ChampionshipDriverEntry {
                    Id = id,
                    Name = section.Get("DRIVER_NAME"),
                    Nationality = section.Get("NATIONALITY"),
                    AiLevel = section.GetInt("AI_LEVEL"),
                    SetupId = section.Get("SETUP"),
                    Car = car,
                    CarSkin = carSkin
                };
            }).Prepend(new ChampionshipDriverEntry {
                Id = -1,
                Name = SettingsHolder.Drive.PlayerName,
                Nationality = SettingsHolder.Drive.PlayerNationality,
                Car = firstEvent?.Car,
                CarSkin = firstEvent?.CarSkin,
                IsPlayer = true
            });
            ChampionshipDrivers.ReplaceEverythingBy(drivers);
        }

        private void Events_CollectionReady(object sender, EventArgs e) {
            if (Type != KunosCareerObjectType.Championship) return;

            var points = Events.Sum(x => ChampionshipPointsPerPlace.ElementAtOrDefault(x.TakenPlace - 1));
            if (points == ChampionshipPoints) return;

            Logging.Write($"Summary points restored: {points} (was {ChampionshipPoints})");
            ChampionshipPoints = points;
            ChampionshipDrivers.GetById(-1).Points = ChampionshipPoints;
            ChampionshipDriversView.Refresh();
        }

        public string DisplayType => Type.GetDescription();

        public bool IsStarted => CompletedEvents > 0;

        public string DisplayGo => CompletedEvents > 0 ? @"Resume" : @"Start";

        public string DisplayRequired => (RequiredAnySeries && RequiredSeries.Length > 1 ? @"Any of " : "") +
                RequiredSeries.Select(x => KunosCareerManager.Instance.GetById(x)?.Code ?? $"<{x}>").JoinToString(", ");

        public override string IniFilename => Path.Combine(Location, "series.ini");

        public string OpponentsIniFilename => Path.Combine(Location, "opponents.ini");

        public string PreviewImage => ImageRefreshing ?? Path.Combine(Location, "preview.png");

        public string StartImage => ImageRefreshing ?? Path.Combine(Location, "start.png");

        public string StartVideo => ImageRefreshing ?? Path.Combine(Location, "start.ogv");

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

        private string _code;

        public string Code {
            get { return _code; }
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
            get { return _type; }
            set {
                if (Equals(value, _type)) return;
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayType));
                ChampionshipResetCommand.OnCanExecuteChanged();
            }
        }

        private string[] _requiredSeries;

        public string[] RequiredSeries {
            get { return _requiredSeries; }
            set {
                if (Equals(value, _requiredSeries)) return;
                _requiredSeries = value;
                OnPropertyChanged();
            }
        }

        private bool _requiredAnySeries;

        public bool RequiredAnySeries {
            get { return _requiredAnySeries; }
            set {
                if (Equals(value, _requiredAnySeries)) return;
                _requiredAnySeries = value;
                OnPropertyChanged();
            }
        }

        private int[] _pointsForPlace;

        public int[] PointsForPlace {
            get { return _pointsForPlace; }
            set {
                if (Equals(value, _pointsForPlace)) return;
                _pointsForPlace = value;
                OnPropertyChanged();
            }
        }

        private int _championshipPointsGoal;

        public int ChampionshipPointsGoal {
            get { return _championshipPointsGoal; }
            set {
                if (Equals(value, _championshipPointsGoal)) return;
                _championshipPointsGoal = value;
                OnPropertyChanged();
            }
        }

        private int _firstPlacesGoal;

        public int FirstPlacesGoal {
            get { return _firstPlacesGoal; }
            set {
                if (Equals(value, _firstPlacesGoal)) return;
                _firstPlacesGoal = value;
                OnPropertyChanged();
            }
        }

        private int _secondPlacesGoal;

        public int SecondPlacesGoal {
            get { return _secondPlacesGoal; }
            set {
                if (Equals(value, _secondPlacesGoal)) return;
                _secondPlacesGoal = value;
                OnPropertyChanged();
            }
        }

        private int _thirdPlacesGoal;

        public int ThirdPlacesGoal {
            get { return _thirdPlacesGoal; }
            set {
                if (Equals(value, _thirdPlacesGoal)) return;
                _thirdPlacesGoal = value;
                OnPropertyChanged();
            }
        }

        private int[] _championshipPointsPerPlace;

        public int[] ChampionshipPointsPerPlace {
            get { return _championshipPointsPerPlace; }
            set {
                if (Equals(value, _championshipPointsPerPlace)) return;
                _championshipPointsPerPlace = value;
                OnPropertyChanged();
            }
        }

        private KunosCareerObject _nextCareerObject;

        public KunosCareerObject NextCareerObject {
            get { return _nextCareerObject; }
            set {
                if (Equals(value, _nextCareerObject)) return;
                _nextCareerObject = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Progress values
        private bool _isAvailable;

        public bool IsAvailable {
            get { return _isAvailable; }
            set {
                if (Equals(value, _isAvailable)) return;
                _isAvailable = value;
                OnPropertyChanged();
            }
        }

        private int _completedEvents;

        public int CompletedEvents {
            get { return _completedEvents; }
            set {
                if (Equals(value, _completedEvents)) return;
                _completedEvents = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayGo));
                OnPropertyChanged(nameof(IsStarted));
                ChampionshipResetCommand.OnCanExecuteChanged();
            }
        }

        private int _firstPlaces;

        public int FirstPlaces {
            get { return _firstPlaces; }
            set {
                if (Equals(value, _firstPlaces)) return;
                _firstPlaces = value;
                OnPropertyChanged();
            }
        }

        private int _secondPlaces;

        public int SecondPlaces {
            get { return _secondPlaces; }
            set {
                if (Equals(value, _secondPlaces)) return;
                _secondPlaces = value;
                OnPropertyChanged();
            }
        }

        private int _thirdPlaces;

        public int ThirdPlaces {
            get { return _thirdPlaces; }
            set {
                if (Equals(value, _thirdPlaces)) return;
                _thirdPlaces = value;
                OnPropertyChanged();
            }
        }

        private int _firstPlacesNeeded;

        public int FirstPlacesNeeded {
            get { return _firstPlacesNeeded; }
            set {
                if (Equals(value, _firstPlacesNeeded)) return;
                _firstPlacesNeeded = value;
                OnPropertyChanged();
            }
        }

        private int _secondPlacesNeeded;

        public int SecondPlacesNeeded {
            get { return _secondPlacesNeeded; }
            set {
                if (Equals(value, _secondPlacesNeeded)) return;
                _secondPlacesNeeded = value;
                OnPropertyChanged();
            }
        }

        private int _thirdPlacesNeeded;

        public int ThirdPlacesNeeded {
            get { return _thirdPlacesNeeded; }
            set {
                if (Equals(value, _thirdPlacesNeeded)) return;
                _thirdPlacesNeeded = value;
                OnPropertyChanged();
            }
        }

        private int _championshipPointsNeeded;

        public int ChampionshipPointsNeeded {
            get { return _championshipPointsNeeded; }
            set {
                if (Equals(value, _championshipPointsNeeded)) return;
                _championshipPointsNeeded = value;
                OnPropertyChanged();
            }
        }

        private bool _isCompleted;

        public bool IsCompleted {
            get { return _isCompleted; }
            set {
                if (Equals(value, _isCompleted)) return;
                _isCompleted = value;
                OnPropertyChanged();
            }
        }

        private long _lastSelectedTimestamp;

        public long LastSelectedTimestamp {
            get { return _lastSelectedTimestamp; }
            set {
                if (Equals(value, _lastSelectedTimestamp)) return;
                _lastSelectedTimestamp = value;
                OnPropertyChanged();
                SaveProgress(false);
            }
        }

        private int _championshipPoints = -1;

        public int ChampionshipPoints {
            get { return _championshipPoints; }
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

        public IReadOnlyList<int> ChampionshipAiPoints {
            get { return _championshipAiPoints; }
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
            get { return _championshipPlace; }
            set {
                if (Equals(value, _championshipPlace)) return;
                _championshipPlace = value;
                OnPropertyChanged();
            }
        }
        #endregion

        protected override void LoadData(IniFile ini) {
            Name = ini["SERIES"].Get("NAME");
            Code = ini["SERIES"].Get("CODE");
            Description = AcStringValues.DecodeDescription(ini["SERIES"].Get("DESCRIPTION"));

            PointsForPlace = ini["SERIES"].Get("POINTS").Split(',').Select(x => FlexibleParser.TryParseInt(x)).OfType<int>().ToArray();
            ChampionshipPointsGoal = ini["GOALS"].GetIntNullable("POINTS") ?? 0;
            ThirdPlacesGoal = ini["GOALS"].GetIntNullable("TIER1") ?? 0;
            SecondPlacesGoal = ini["GOALS"].GetIntNullable("TIER2") ?? 0;
            FirstPlacesGoal = ini["GOALS"].GetIntNullable("TIER3") ?? 0;
            Type = ChampionshipPointsGoal == 0 || PointsForPlace.Sum() == 0 ? KunosCareerObjectType.SingleEvents : KunosCareerObjectType.Championship;

            RequiredSeries = ini["SERIES"].GetStrings("REQUIRES").ToArray();
            RequiredAnySeries = ini["SERIES"].GetBool("REQUIRESANY");

            ChampionshipPointsPerPlace = ini["SERIES"].GetStrings("POINTS").Select(x => FlexibleParser.TryParseInt(x)).OfType<int>().ToArray();

            if (Type == KunosCareerObjectType.Championship) {
                LoadOpponents();
            }

            LoadProgress();
        }

        public override void SaveData(IniFile ini) {
            ini["SERIES"].Set("NAME", Name);
            ini["SERIES"].Set("CODE", Code);
            ini["SERIES"].Set("DESCRIPTION", AcStringValues.EncodeDescription(Description));
            ini["SERIES"].Set("POINTS", PointsForPlace.JoinToString(","));
            ini["GOALS"].Set("POINTS", ChampionshipPointsGoal);
            ini["GOALS"].Set("TIER1", ThirdPlacesGoal);
            ini["GOALS"].Set("TIER2", SecondPlacesGoal);
            ini["GOALS"].Set("TIER3", FirstPlacesGoal);
            ini["SERIES"].Set("REQUIRES", RequiredSeries);
            ini["SERIES"].Set("REQUIRESANY", RequiredAnySeries);
        }

        private void LoadProgressFromEntry() {
            Func<string, bool> fn = x => KunosCareerProgress.Instance.Completed.Contains(x);
            IsAvailable = RequiredSeries.Length == 0 || (RequiredAnySeries ? RequiredSeries.Any(fn) : RequiredSeries.All(fn));
            IsCompleted = KunosCareerProgress.Instance.Completed.Contains(Id);

            var entry = KunosCareerProgress.Instance.Entries.GetValueOrDefault(Id);
            if (entry == null) {
                CompletedEvents = 0;
                FirstPlaces = 0;
                SecondPlaces = 0;
                ThirdPlaces = 0;
                ChampionshipAiPoints = new int[0];
                ChampionshipPoints = 0;
                LastSelectedTimestamp = 0;
                
                foreach (var driver in ChampionshipDrivers) {
                    driver.Points = 0;
                }
                ChampionshipDriversView.Refresh();
                return;
            }

            foreach (var driver in ChampionshipDrivers) {
                driver.Points = 0;
            }

            CompletedEvents = entry.EventsResults.Count(x => x > 0);
            if (Type == KunosCareerObjectType.SingleEvents) {
                FirstPlaces = entry.EventsResults.Count(x => x == 3);
                SecondPlaces = entry.EventsResults.Count(x => x == 2);
                ThirdPlaces = entry.EventsResults.Count(x => x == 1);
                ChampionshipAiPoints = new int[0];
                ChampionshipPoints = 0;
            } else {
                FirstPlaces = entry.EventsResults.Count(x => x == 1);
                SecondPlaces = entry.EventsResults.Count(x => x == 2);
                ThirdPlaces = entry.EventsResults.Count(x => x == 3);
                ChampionshipAiPoints = entry.AiPoints;
                ChampionshipPoints = entry.Points ?? 0;

                for (var i = 0; i < ChampionshipAiPoints.Count; i++) {
                    var driverEntry = ChampionshipDrivers.GetByIdOrDefault(i);
                    if (driverEntry != null) {
                        driverEntry.Points = ChampionshipAiPoints[i];
                    } else {
                        Logging.Warning("[KUNOSCAREEROBJECT] Missing driver entry with ID=" + i);
                    }
                }

                ChampionshipDrivers.GetById(-1).Points = ChampionshipPoints;
            }

            ChampionshipDriversView.Refresh();

            _lastSelectedTimestamp = entry.LastSelectedTimestamp;
            OnPropertyChanged(nameof(LastSelectedTimestamp));

            if (EventsManager?.IsScanned == true) {
                _selectedEvent = EventsManager.GetByNumber(entry.SelectedEvent) ?? _selectedEvent;
                OnPropertyChanged(nameof(SelectedEvent));
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
                                    events.Select(x => x.TakenPlace).TakeWhile(x => x != 0),
                                    ChampionshipPoints,
                                    ChampionshipAiPoints),
                            globalUpdate);
                    break;
                case KunosCareerObjectType.SingleEvents:
                    KunosCareerProgress.Instance.UpdateEntry(Id,
                            new KunosCareerProgressEntry(
                                    SelectedEvent?.EventNumber ?? 0,
                                    Events.Select(x => 4 - x.TakenPlace),
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

        private RelayPropertyCommand _championshipResetCommand;

        public RelayPropertyCommand ChampionshipResetCommand => _championshipResetCommand ?? (_championshipResetCommand = new RelayPropertyCommand(o => {
            KunosCareerProgress.Instance.UpdateEntry(Id, new KunosCareerProgressEntry(0, new int[0], 0, new int[0]), true);
            KunosCareerProgress.Instance.Completed = KunosCareerProgress.Instance.Completed.ApartFrom(Id).ToArray();
        }, o => Type == KunosCareerObjectType.Championship && IsStarted));
    }
}
