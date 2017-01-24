using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public enum ServerPresetAssistState {
        Denied = 0,
        Factory = 1,
        Forced = 2
    }

    public enum ServerPresetJumpStart {
        [LocalizedDescription("JumpStart_CarLocked")]
        CarLocked = 0,

        [LocalizedDescription("JumpStart_TeleportToPit")]
        TeleportToPit = 1,

        [LocalizedDescription("JumpStart_DriveThrough")]
        DriveThrough = 2
    }

    public class ServerSessionEntry : Displayable {
        private readonly string _onKey, _offKey;

        public sealed override string DisplayName { get; set; }

        public ServerSessionEntry([Localizable(false)] string key, string name, bool isClosable) {
            _onKey = key;
            _offKey = $@"__CM_{_onKey}_OFF";

            DisplayName = name;
            IsClosable = isClosable;
        }

        public void Load(IniFile config) {
            IsEnabled = config.ContainsKey(_onKey);
            Load(IsEnabled ? config[_onKey] : config[_offKey]);
        }

        protected virtual void Load(IniFileSection section) {
            ConfigName = section.GetNonEmpty("NAME") ?? DisplayName;
            Time = TimeSpan.FromMinutes(section.GetDouble("TIME", 5d));
            IsOpen = section.GetBool("IS_OPEN", true);
        }

        public void Save(IniFile config) {
            if (IsEnabled) {
                Save(config[_onKey]);
                config.Remove(_offKey);
            } else {
                Save(config[_offKey]);
                config.Remove(_onKey);
            }
        }

        protected virtual void Save(IniFileSection section) {
            section.Set("NAME", string.IsNullOrWhiteSpace(ConfigName) ? DisplayName : ConfigName);
            section.Set("TIME", Time.TotalMinutes); // round?
            section.Set("IS_OPEN", IsOpen);
        }

        private string _configName;

        public string ConfigName {
            get { return _configName; }
            set {
                if (Equals(value, _configName)) return;
                _configName = value;
                OnPropertyChanged();
            }
        }

        private bool _isEnabled;

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _time;
        
        public TimeSpan Time {
            get { return _time; }
            set {
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
            }
        }

        private bool _isOpen;

        public bool IsOpen {
            get { return _isOpen; }
            set {
                if (Equals(value, _isOpen)) return;
                _isOpen = value;
                OnPropertyChanged();
            }
        }

        public bool IsClosable { get; }
    }

    public enum ServerPresetRaceJoinType {
        [Description("Close")]
        Close,

        [Description("Open")]
        Open,

        [Description("Close at start")]
        CloseAtStart
    }

    public class ServerRaceSessionEntry : ServerSessionEntry {
        public ServerRaceSessionEntry() : base("RACE", ToolsStrings.Session_Race, true) { }

        protected override void Load(IniFileSection section) {
            base.Load(section);
            LapsCount = section.GetInt("LAPS", 5);
            WaitTime = TimeSpan.FromSeconds(section.GetInt("WAIT_TIME", 60));
            JoinType = section.GetIntEnum("IS_OPEN", ServerPresetRaceJoinType.CloseAtStart);
        }

        protected override void Save(IniFileSection section) {
            base.Save(section);
            section.Set("LAPS", LapsCount);
            section.Set("WAIT_TIME", WaitTime.TotalSeconds); // round?
            section.SetIntEnum("IS_OPEN", JoinType);
        }

        private int _lapsCount;

        public int LapsCount {
            get { return _lapsCount; }
            set {
                if (Equals(value, _lapsCount)) return;
                _lapsCount = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _waitTime;

        public TimeSpan WaitTime {
            get { return _waitTime; }
            set {
                if (Equals(value, _waitTime)) return;
                _waitTime = value;
                OnPropertyChanged();
            }
        }

        private ServerPresetRaceJoinType _joinType;

        public ServerPresetRaceJoinType JoinType {
            get { return _joinType; }
            set {
                if (Equals(value, _joinType)) return;
                _joinType = value;
                OnPropertyChanged();
            }
        }
    }

    public class ServerWeatherEntry : NotifyPropertyChanged, IDraggable {
        private int _index;

        public int Index {
            get { return _index; }
            set {
                if (Equals(value, _index)) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        public ServerWeatherEntry() {
            WeatherId = WeatherManager.Instance.GetDefault()?.Id;
            BaseAmbientTemperature = 18d;
            BaseRoadTemperature = 6d;
            AmbientTemperatureVariation = 2d;
            RoadTemperatureVariation = 1d;
        }

        public ServerWeatherEntry(IniFileSection section) {
            WeatherId = section.GetNonEmpty("GRAPHICS");
            BaseAmbientTemperature = section.GetDouble("BASE_TEMPERATURE_AMBIENT", 18d);
            BaseRoadTemperature = section.GetDouble("BASE_TEMPERATURE_ROAD", 6d);
            AmbientTemperatureVariation = section.GetDouble("VARIATION_AMBIENT", 2d);
            RoadTemperatureVariation = section.GetDouble("VARIATION_ROAD", 1d);
        }

        public void SaveTo(IniFileSection section) {
            section.Set("GRAPHICS", WeatherId);
            section.Set("BASE_TEMPERATURE_AMBIENT", BaseAmbientTemperature);
            section.Set("BASE_TEMPERATURE_ROAD", BaseRoadTemperature);
            section.Set("VARIATION_AMBIENT", AmbientTemperatureVariation);
            section.Set("VARIATION_ROAD", RoadTemperatureVariation);
        }

        private string _weatherId;

        [CanBeNull]
        public string WeatherId {
            get { return _weatherId; }
            set {
                if (Equals(value, _weatherId)) return;
                _weatherSet = false;
                _weather = null;
                _weatherId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Weather));
                OnPropertyChanged(nameof(RecommendedRoadTemperature));
            }
        }

        private bool _weatherSet;
        private WeatherObject _weather;
        
        [CanBeNull]
        public WeatherObject Weather {
            get {
                if (!_weatherSet) {
                    _weatherSet = true;
                    _weather = WeatherId == null ? null : WeatherManager.Instance.GetById(WeatherId);
                }
                return _weather;
            }
            set { WeatherId = value?.Id; }
        }

        private double _baseAmbientTemperature;

        public double BaseAmbientTemperature {
            get { return _baseAmbientTemperature; }
            set {
                value = value.Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum).Round(0.1);
                if (Equals(value, _baseAmbientTemperature)) return;
                _baseAmbientTemperature = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecommendedRoadTemperature));
            }
        }

        private double _baseRoadTemperature;

        public double BaseRoadTemperature {
            get { return _baseRoadTemperature; }
            set {
                value = value.Clamp(CommonAcConsts.RoadTemperatureMinimum, CommonAcConsts.RoadTemperatureMaximum).Round(0.1);
                if (Equals(value, _baseRoadTemperature)) return;
                _baseRoadTemperature = value;
                OnPropertyChanged();
            }
        }

        private double _ambientTemperatureVariation;

        public double AmbientTemperatureVariation {
            get { return _ambientTemperatureVariation; }
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _ambientTemperatureVariation)) return;
                _ambientTemperatureVariation = value;
                OnPropertyChanged();
            }
        }

        private double _roadTemperatureVariation;

        public double RoadTemperatureVariation {
            get { return _roadTemperatureVariation; }
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _roadTemperatureVariation)) return;
                _roadTemperatureVariation = value;
                OnPropertyChanged();
            }
        }

        private int _time;

        internal int Time {
            set {
                _time = value;
                OnPropertyChanged(nameof(RecommendedRoadTemperature));
            }
        }

        public double RecommendedRoadTemperature =>
                Game.ConditionProperties.GetRoadTemperature(_time, BaseAmbientTemperature, Weather?.TemperatureCoefficient ?? 1d);

        private bool _deleted;

        public bool Deleted {
            get { return _deleted; }
            set {
                if (Equals(value, _deleted)) return;
                _deleted = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
        }));

        public const string DraggableFormat = "Data-ServerWeatherEntry";

        string IDraggable.DraggableFormat => DraggableFormat;
    }

    public class ServerPresetObject : AcIniObject {
        public ServerPresetObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            Sessions = new ChangeableObservableCollection<ServerSessionEntry>(new[] {
                new ServerSessionEntry("BOOK", ToolsStrings.Session_Booking, false), 
                new ServerSessionEntry("PRACTICE", ToolsStrings.Session_Practice, true), 
                new ServerSessionEntry("QUALIFY", ToolsStrings.Session_Qualification, true), 
                new ServerRaceSessionEntry(), 
            });
        }

        protected override IniFileMode IniFileMode => IniFileMode.ValuesWithSemicolons;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "server_cfg.ini");
            EntryListIniFilename = Path.Combine(Location, "entry_list.ini");
            ResultsDirectory = Path.Combine(Location, "results");
        }

        public string EntryListIniFilename { get; private set; }

        public string ResultsDirectory { get; private set; }

        #region Data
        #region Common fields
        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _trackId)) return;
                _trackId = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    // TODO: missing track id error
                }
            }
        }
        
        private string[] _carIds;

        [NotNull]
        public string[] CarIds {
            get { return _carIds; }
            set {
                if (Equals(value, _carIds) || _carIds != null && value.Length == _carIds.Length &&
                        value.All((x, i) => Equals(_carIds[i], x))) return;
                _carIds = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    // TODO: missing car ids error
                }
            }
        }

        private string _trackLayoutId;

        public string TrackLayoutId {
            get { return _trackLayoutId; }
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _trackLayoutId)) return;
                _trackLayoutId = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _sendIntervalHz;

        public int SendIntervalHz {
            get { return _sendIntervalHz; }
            set {
                value = value.Clamp(10, 35);
                if (Equals(value, _sendIntervalHz)) return;
                _sendIntervalHz = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _capacity;

        public int Capacity {
            get { return _capacity; }
            set {
                value = value.Clamp(1, 200);
                if (Equals(value, _capacity)) return;
                _capacity = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _threads;

        public int Threads {
            get { return _threads; }
            set {
                value = value.Clamp(2, 48);
                if (Equals(value, _threads)) return;
                _threads = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _udpPort;

        public int UdpPort {
            get { return _udpPort; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _udpPort)) return;
                _udpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _tcpPort;

        public int TcpPort {
            get { return _tcpPort; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _tcpPort)) return;
                _tcpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _httpPort;

        public int HttpPort {
            get { return _httpPort; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _httpPort)) return;
                _httpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _showOnLobby;

        public bool ShowOnLobby {
            get { return _showOnLobby; }
            set {
                if (Equals(value, _showOnLobby)) return;
                _showOnLobby = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _loopMode;

        public bool LoopMode {
            get { return _loopMode; }
            set {
                if (Equals(value, _loopMode)) return;
                _loopMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _pickupMode;

        public bool PickupMode {
            get { return _pickupMode; }
            set {
                if (Equals(value, _pickupMode)) return;
                _pickupMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private TimeSpan _raceOverTime;

        public TimeSpan RaceOverTime {
            get { return _raceOverTime; }
            set {
                if (Equals(value, _raceOverTime)) return;
                _raceOverTime = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _password;

        [CanBeNull]
        public string Password {
            get { return _password; }
            set {
                if (Equals(value, _password)) return;
                _password = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _adminPassword;

        [CanBeNull]
        public string AdminPassword {
            get { return _adminPassword; }
            set {
                if (Equals(value, _adminPassword)) return;
                _adminPassword = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetAssistState _abs;

        public ServerPresetAssistState Abs {
            get { return _abs; }
            set {
                if (Equals(value, _abs)) return;
                _abs = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetAssistState _tractionControl;

        public ServerPresetAssistState TractionControl {
            get { return _tractionControl; }
            set {
                if (Equals(value, _tractionControl)) return;
                _tractionControl = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _stabilityControl;

        public bool StabilityControl {
            get { return _stabilityControl; }
            set {
                if (Equals(value, _stabilityControl)) return;
                _stabilityControl = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _autoClutch;

        public bool AutoClutch {
            get { return _autoClutch; }
            set {
                if (Equals(value, _autoClutch)) return;
                _autoClutch = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _tyreBlankets;

        public bool TyreBlankets {
            get { return _tyreBlankets; }
            set {
                if (Equals(value, _tyreBlankets)) return;
                _tyreBlankets = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _forceVirtualMirror;

        public bool ForceVirtualMirror {
            get { return _forceVirtualMirror; }
            set {
                if (Equals(value, _forceVirtualMirror)) return;
                _forceVirtualMirror = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _fuelRate;

        public int FuelRate {
            get { return _fuelRate; }
            set {
                value = value.Clamp(0, 500);
                if (Equals(value, _fuelRate)) return;
                _fuelRate = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _damageRate;

        public int DamageRate {
            get { return _damageRate; }
            set {
                value = value.Clamp(0, 400);
                if (Equals(value, _damageRate)) return;
                _damageRate = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _tyreWearRate;

        public int TyreWearRate {
            get { return _tyreWearRate; }
            set {
                value = value.Clamp(0, 500);
                if (Equals(value, _tyreWearRate)) return;
                _tyreWearRate = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _allowTyresOut;

        public int AllowTyresOut {
            get { return _allowTyresOut; }
            set {
                value = value.Clamp(0, 4);
                if (Equals(value, _allowTyresOut)) return;
                _allowTyresOut = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _maxBallast;

        public int MaxBallast {
            get { return _maxBallast; }
            set {
                value = value.Clamp(0, 300);
                if (Equals(value, _maxBallast)) return;
                _maxBallast = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _qualifyLimitPercentage;

        public int QualifyLimitPercentage {
            get { return _qualifyLimitPercentage; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _qualifyLimitPercentage)) return;
                _qualifyLimitPercentage = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetJumpStart _jumpStart;

        public ServerPresetJumpStart JumpStart {
            get { return _jumpStart; }
            set {
                if (Equals(value, _jumpStart)) return;
                _jumpStart = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion

        #region Sessions and conditions
        private ChangeableObservableCollection<ServerSessionEntry> _sessions;

        public ChangeableObservableCollection<ServerSessionEntry> Sessions {
            get { return _sessions; }
            set {
                if (Equals(value, _sessions)) return;

                if (_sessions != null) {
                    // _sessions.CollectionChanged -= OnSessionEntriesCollectionChanged;
                    _sessions.ItemPropertyChanged -= OnSessionEntryPropertyChanged;
                }

                _sessions = value;
                OnPropertyChanged();

                if (_sessions != null) {
                    // _sessions.CollectionChanged += OnSessionEntriesCollectionChanged;
                    _sessions.ItemPropertyChanged += OnSessionEntryPropertyChanged;
                }
            }
        }

        private void OnSessionEntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (Loaded) {
                Changed = true;
            }
        }
        #endregion

        #region Driver entries
        private ChangeableObservableCollection<ServerPresetDriverEntry> _driverEntries;

        public ChangeableObservableCollection<ServerPresetDriverEntry> DriverEntries {
            get { return _driverEntries; }
            set {
                if (Equals(value, _driverEntries)) return;

                if (_driverEntries != null) {
                    _driverEntries.CollectionChanged -= OnDriverEntriesCollectionChanged;
                    _driverEntries.ItemPropertyChanged -= OnDriverEntryPropertyChanged;
                }

                _driverEntries = value;
                OnPropertyChanged();

                if (_driverEntries != null) {
                    _driverEntries.CollectionChanged += OnDriverEntriesCollectionChanged;
                    _driverEntries.ItemPropertyChanged += OnDriverEntryPropertyChanged;
                }
            }
        }

        private void OnDriverEntriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs) {
            CarIds = _driverEntries.Select(x => x.CarId).Distinct().ToArray();
            if (Loaded) {
                Changed = true;
            }
        }

        private void OnDriverEntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerPresetDriverEntry.Deleted):
                    DriverEntries.Remove((ServerPresetDriverEntry)sender);
                    return;
            }

            if (Loaded) {
                Changed = true;
            }
        }
        #endregion

        #region Conditions
        private int _time;

        public int Time {
            get { return _time; }
            set {
                value = value.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SunAngle));
                OnPropertyChanged(nameof(DisplayTime));

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;

                    UpdateWeatherIndexes();
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

        public double SunAngle {
            get { return Game.ConditionProperties.GetSunAngle(Time); }
            set { Time = Game.ConditionProperties.GetSeconds(value).RoundToInt(); }
        }

        private bool _dynamicTrackEnabled;

        public bool DynamicTrackEnabled {
            get { return _dynamicTrackEnabled; }
            set {
                if (Equals(value, _dynamicTrackEnabled)) return;
                _dynamicTrackEnabled = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private Game.TrackProperties _trackProperties;

        public Game.TrackProperties TrackProperties {
            get { return _trackProperties; }
            set {
                if (Equals(value, _trackProperties)) return;
                _trackProperties = value;
                OnPropertyChanged();
            }
        }

        private ChangeableObservableCollection<ServerWeatherEntry> _weather;

        public ChangeableObservableCollection<ServerWeatherEntry> Weather {
            get { return _weather; }
            set {
                if (Equals(value, _weather)) return;

                if (_weather != null) {
                    _weather.CollectionChanged -= OnWeatherCollectionChanged;
                    _weather.ItemPropertyChanged -= OnWeatherPropertyChanged;
                }

                _weather = value;
                OnPropertyChanged();

                if (_weather != null) {
                    _weather.CollectionChanged += OnWeatherCollectionChanged;
                    _weather.ItemPropertyChanged += OnWeatherPropertyChanged;
                    UpdateWeatherIndexes();
                }
            }
        }

        private void OnWeatherCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs) {
            UpdateWeatherIndexes();
            if (Loaded) {
                Changed = true;
            }
        }

        private void UpdateWeatherIndexes() {
            if (Weather == null) return;
            for (var i = 0; i < Weather.Count; i++) {
                var weather = Weather[i];
                weather.Index = i;
                weather.Time = Time;
            }
        }

        private void OnWeatherPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerWeatherEntry.Index):
                case nameof(ServerWeatherEntry.RecommendedRoadTemperature):
                    return;
                case nameof(ServerWeatherEntry.Deleted):
                    Weather.Remove((ServerWeatherEntry)sender);
                    return;
            }

            if (Loaded) {
                Changed = true;
            }
        }
        #endregion

        #region Voting and banning
        private int _kickVoteQuorum;

        public int KickVoteQuorum {
            get { return _kickVoteQuorum; }
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _kickVoteQuorum)) return;
                _kickVoteQuorum = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _sessionVoteQuorum;

        public int SessionVoteQuorum {
            get { return _sessionVoteQuorum; }
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _sessionVoteQuorum)) return;
                _sessionVoteQuorum = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private TimeSpan _voteDuration;

        public TimeSpan VoteDuration {
            get { return _voteDuration; }
            set {
                if (Equals(value, _voteDuration)) return;
                _voteDuration = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _blacklistMode;

        public bool BlacklistMode {
            get { return _blacklistMode; }
            set {
                if (Equals(value, _blacklistMode)) return;
                _blacklistMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _maxCollisionsPerKm;

        public int MaxCollisionsPerKm {
            get { return _maxCollisionsPerKm; }
            set {
                value = value.Clamp(-1, 128);
                if (Equals(value, _maxCollisionsPerKm)) return;
                _maxCollisionsPerKm = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion
        #endregion

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            LoadEntryListOrThrow();
        }

        private IniFile _entryListIniObject;

        public IniFile EntryListIniObject {
            get { return _entryListIniObject; }
            set {
                if (Equals(value, _entryListIniObject)) return;
                _entryListIniObject = value;
                OnPropertyChanged();
            }
        }

        private void LoadEntryListOrThrow() {
            string text;

            try {
                text = FileUtils.ReadAllText(EntryListIniFilename);
            } catch (FileNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(EntryListIniFilename));
                return;
            } catch (DirectoryNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(EntryListIniFilename));
                return;
            }

            try {
                EntryListIniObject = IniFile.Parse(text, IniFileMode);
            } catch (Exception) {
                EntryListIniObject = null;
                AddError(AcErrorType.Data_IniIsDamaged, Path.GetFileName(EntryListIniFilename));
                return;
            }

            try {
                LoadEntryListData(EntryListIniObject);
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        protected override void LoadData(IniFile ini) {
            var section = ini["SERVER"];
            Name = section.GetPossiblyEmpty("NAME");
            Password = section.GetNonEmpty("PASSWORD");
            AdminPassword = section.GetNonEmpty("ADMIN_PASSWORD");
            ShowOnLobby = section.GetBool("REGISTER_TO_LOBBY", true);
            LoopMode = section.GetBool("LOOP_MODE", true);
            PickupMode = section.GetBool("PICKUP_MODE_ENABLED", false);
            RaceOverTime = TimeSpan.FromSeconds(section.GetDouble("RACE_OVER_TIME", 60d));
            Capacity = section.GetInt("MAX_CLIENTS", 3);

            UdpPort = section.GetInt("UDP_PORT", 9600);
            TcpPort = section.GetInt("TCP_PORT", 9600);
            HttpPort = section.GetInt("HTTP_PORT", 8081);
            SendIntervalHz = section.GetInt("CLIENT_SEND_INTERVAL_HZ", 18);
            Threads = section.GetInt("NUM_THREADS", 2);

            TrackId = section.GetNonEmpty("TRACK");
            TrackLayoutId = section.GetNonEmpty("CONFIG_TRACK");
            CarIds = section.GetStrings("CARS", ';');

            Abs = section.GetIntEnum("ABS_ALLOWED", ServerPresetAssistState.Factory);
            TractionControl = section.GetIntEnum("TC_ALLOWED", ServerPresetAssistState.Factory);
            StabilityControl = section.GetBool("STABILITY_ALLOWED", false);
            AutoClutch = section.GetBool("AUTOCLUTCH_ALLOWED", false);
            TyreBlankets = section.GetBool("TYRE_BLANKETS_ALLOWED", false);
            ForceVirtualMirror = section.GetBool("FORCE_VIRTUAL_MIRROR", true);

            FuelRate = section.GetInt("FUEL_RATE", 100);
            DamageRate = section.GetInt("DAMAGE_MULTIPLIER", 100);
            TyreWearRate = section.GetInt("TYRE_WEAR_RATE", 100);
            AllowTyresOut = section.GetInt("ALLOWED_TYRES_OUT", 2);
            MaxBallast = section.GetInt("MAX_BALLAST_KG", 0);
            QualifyLimitPercentage = section.GetInt("QUALIFY_MAX_WAIT_PERC", 120);
            JumpStart = section.GetIntEnum("START_RULE", ServerPresetJumpStart.CarLocked);

            foreach (var session in Sessions) {
                session.Load(ini);
            }

            SunAngle = section.GetDouble("SUN_ANGLE", 0d);
            DynamicTrackEnabled = ini.ContainsKey(@"DYNAMIC_TRACK");
            Weather = new ChangeableObservableCollection<ServerWeatherEntry>(ini.GetSections("WEATHER").Select(x => new ServerWeatherEntry(x)));
            TrackProperties = Game.TrackProperties.Load(DynamicTrackEnabled ? ini["DYNAMIC_TRACK"] : ini["__CM_DYNAMIC_TRACK_OFF"]);

            KickVoteQuorum = section.GetInt("KICK_QUORUM", 85);
            SessionVoteQuorum = section.GetInt("VOTING_QUORUM", 80);
            VoteDuration = TimeSpan.FromSeconds(section.GetDouble("VOTE_DURATION", 20d));
            BlacklistMode = section.GetBool("BLACKLIST_MODE", true);
            MaxCollisionsPerKm = section.GetInt("MAX_CONTACTS_PER_KM", -1);
        }

        private void LoadEntryListData(IniFile ini) {
            DriverEntries = new ChangeableObservableCollection<ServerPresetDriverEntry>(ini.GetSections("CAR").Select(x => new ServerPresetDriverEntry(x)));
        }

        public override void SaveData(IniFile ini) {
            var section = ini["SERVER"];
            section.Set("NAME", Name);
            section.Set("PASSWORD", Password);
            section.Set("ADMIN_PASSWORD", AdminPassword);
            section.Set("REGISTER_TO_LOBBY", ShowOnLobby);
            section.Set("LOOP_MODE", LoopMode);
            section.Set("PICKUP_MODE_ENABLED", PickupMode);
            section.Set("RACE_OVER_TIME", RaceOverTime.TotalSeconds.RoundToInt());
            section.Set("MAX_CLIENTS", Capacity);

            section.Set("UDP_PORT", UdpPort);
            section.Set("TCP_PORT", TcpPort);
            section.Set("HTTP_PORT", HttpPort);
            section.Set("CLIENT_SEND_INTERVAL_HZ", SendIntervalHz);
            section.Set("NUM_THREADS", Threads);

            section.Set("TRACK", TrackId);
            section.Set("CONFIG_TRACK", TrackLayoutId);
            section.Set("CARS", CarIds, ';');

            section.SetIntEnum("ABS_ALLOWED", Abs);
            section.SetIntEnum("TC_ALLOWED", TractionControl);
            section.Set("STABILITY_ALLOWED", StabilityControl);
            section.Set("AUTOCLUTCH_ALLOWED", AutoClutch);
            section.Set("TYRE_BLANKETS_ALLOWED", TyreBlankets);
            section.Set("FORCE_VIRTUAL_MIRROR", ForceVirtualMirror);

            section.Set("FUEL_RATE", FuelRate);
            section.Set("DAMAGE_MULTIPLIER", DamageRate);
            section.Set("TYRE_WEAR_RATE", TyreWearRate);
            section.Set("ALLOWED_TYRES_OUT", AllowTyresOut);
            section.Set("MAX_BALLAST_KG", MaxBallast);
            section.Set("QUALIFY_MAX_WAIT_PERC", QualifyLimitPercentage);
            section.SetIntEnum("START_RULE", JumpStart);

            foreach (var session in Sessions) {
                session.Save(ini);
            }

            section.Set("SUN_ANGLE", SunAngle.RoundToInt());
            ini.SetSections("WEATHER", Weather, (e, s) => e.SaveTo(s));
            if (DynamicTrackEnabled) {
                ini.Remove("__CM_DYNAMIC_TRACK_OFF");
                (TrackProperties ?? Game.GetDefaultTrackPropertiesPreset().Properties).Set(ini["DYNAMIC_TRACK"]);
            } else {
                ini.Remove("DYNAMIC_TRACK");
                (TrackProperties ?? Game.GetDefaultTrackPropertiesPreset().Properties).Set(ini["__CM_DYNAMIC_TRACK_OFF"]);
            }

            section.Set("KICK_QUORUM", KickVoteQuorum);
            section.Set("VOTING_QUORUM", 80);
            section.Set("VOTE_DURATION", VoteDuration.TotalSeconds.RoundToInt());
            section.Set("BLACKLIST_MODE", BlacklistMode);
            section.Set("MAX_CONTACTS_PER_KM", MaxCollisionsPerKm);
        }

        public override void Save() {
            EntryListIniObject.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section));
            using ((FileAcManager as IIgnorer)?.IgnoreChanges()) {
                File.WriteAllText(EntryListIniFilename, EntryListIniObject.ToString());
                base.Save();
            }
        }
    }
}
