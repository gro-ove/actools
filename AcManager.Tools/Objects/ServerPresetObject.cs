using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject : AcIniObject {
        public ServerPresetObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            Sessions = new ChangeableObservableCollection<ServerSessionEntry>(new[] {
                new ServerSessionEntry("BOOK", ToolsStrings.Session_Booking, false, false), 
                new ServerSessionEntry("PRACTICE", ToolsStrings.Session_Practice, true, true), 
                new ServerSessionEntry("QUALIFY", ToolsStrings.Session_Qualification, true, true), 
                new ServerRaceSessionEntry("RACE", ToolsStrings.Session_Race, true, true), 
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

        [CanBeNull]
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
                if (!IsPickupModeAvailable) value = false;
                if (Equals(value, _pickupMode)) return;
                _pickupMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;

                    Sessions.GetById("BOOK").IsAvailable = !value;
                }
            }
        }

        private bool _isPickupModeAvailable;

        public bool IsPickupModeAvailable {
            get { return _isPickupModeAvailable; }
            set {
                if (Equals(value, _isPickupModeAvailable)) return;
                _isPickupModeAvailable = value;
                OnPropertyChanged();

                if (!value) {
                    PickupMode = false;
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
            switch (e.PropertyName) {
                case nameof(ServerSessionEntry.IsAvailable):
                    return;
                case nameof(ServerSessionEntry.IsEnabled):
                    if (((IWithId)sender).Id == @"BOOK") {
                        IsPickupModeAvailable = !((ServerSessionEntry)sender).IsEnabled;
                    }
                    break;
            }

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

        private void UpdateCarIds() {
            CarIds = _driverEntries.Select(x => x.CarId).Distinct().ToArray();
        }

        private void OnDriverEntriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs) {
            UpdateCarIds();
            if (Loaded) {
                Changed = true;
            }
        }

        private void OnDriverEntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerPresetDriverEntry.CarId):
                    UpdateCarIds();
                    break;
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

        [CanBeNull]
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
                ResetEntryListData();
                return;
            } catch (DirectoryNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(EntryListIniFilename));
                ResetEntryListData();
                return;
            }

            try {
                EntryListIniObject = IniFile.Parse(text, IniFileMode);
            } catch (Exception) {
                AddError(AcErrorType.Data_IniIsDamaged, Path.GetFileName(EntryListIniFilename));
                ResetEntryListData();
                return;
            }

            try {
                LoadEntryListData(EntryListIniObject);
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        protected override void LoadData(IniFile ini) {
            foreach (var session in Sessions) {
                session.Load(ini);
            }

            IsPickupModeAvailable = !Sessions.GetById(@"BOOK").IsEnabled;

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

            TrackId = section.GetNonEmpty("TRACK", "imola");
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

            SunAngle = section.GetDouble("SUN_ANGLE", 0d);
            DynamicTrackEnabled = ini.ContainsKey(@"DYNAMIC_TRACK");
            TrackProperties = Game.TrackProperties.Load(DynamicTrackEnabled ? ini["DYNAMIC_TRACK"] : ini["__CM_DYNAMIC_TRACK_OFF"]);

            KickVoteQuorum = section.GetInt("KICK_QUORUM", 85);
            SessionVoteQuorum = section.GetInt("VOTING_QUORUM", 80);
            VoteDuration = TimeSpan.FromSeconds(section.GetDouble("VOTE_DURATION", 20d));
            BlacklistMode = section.GetBool("BLACKLIST_MODE", true);
            MaxCollisionsPerKm = section.GetInt("MAX_CONTACTS_PER_KM", -1);

            // At least one weather entry is needed in order to launch the server
            var weather = new ChangeableObservableCollection<ServerWeatherEntry>(ini.GetSections("WEATHER").Select(x => new ServerWeatherEntry(x)));
            if (weather.Count == 0) {
                weather.Add(new ServerWeatherEntry());
            }
            Weather = weather;
        }

        private void LoadEntryListData(IniFile ini) {
            DriverEntries = new ChangeableObservableCollection<ServerPresetDriverEntry>(ini.GetSections("CAR").Select(x => new ServerPresetDriverEntry(x)));
        }

        private void ResetEntryListData() {
            EntryListIniObject = null;
            LoadEntryListData(IniFile.Empty);
        }

        public override void SaveData(IniFile ini) {
            foreach (var session in Sessions) {
                session.Save(ini);
            }

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
            section.Set("VOTING_QUORUM", SessionVoteQuorum);
            section.Set("VOTE_DURATION", VoteDuration.TotalSeconds.RoundToInt());
            section.Set("BLACKLIST_MODE", BlacklistMode);
            section.Set("MAX_CONTACTS_PER_KM", MaxCollisionsPerKm);
        }

        public override void Save() {
            var ini = EntryListIniObject ?? IniFile.Empty;
            ini.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section));
            using ((FileAcManager as IIgnorer)?.IgnoreChanges()) {
                File.WriteAllText(EntryListIniFilename, ini.ToString());
                base.Save();
            }

            RemoveError(AcErrorType.Data_IniIsMissing);
        }
    }
}
