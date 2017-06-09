using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.VisualBasic.Logging;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        #region Data
        #region Common fields
        private string _trackId;

        public string TrackId {
            get => _trackId;
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
            get => _carIds;
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
            get => _trackLayoutId;
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
            get => _sendIntervalHz;
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
            get => _capacity;
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
            get => _threads;
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
            get => _udpPort;
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
            get => _tcpPort;
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
            get => _httpPort;
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
            get => _showOnLobby;
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
            get => _loopMode;
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
            get => _pickupMode;
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

        private bool _pickupModeLockedEntryList;

        public bool PickupModeLockedEntryList {
            get => _pickupModeLockedEntryList;
            set {
                if (Equals(value, _pickupModeLockedEntryList)) return;
                _pickupModeLockedEntryList = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _isPickupModeAvailable;

        public bool IsPickupModeAvailable {
            get => _isPickupModeAvailable;
            set {
                if (Equals(value, _isPickupModeAvailable)) return;
                _isPickupModeAvailable = value;
                OnPropertyChanged();

                if (!value) {
                    PickupMode = false;
                }
            }
        }

        private TimeSpan _resultScreenTime;

        public TimeSpan ResultScreenTime {
            get { return _resultScreenTime; }
            set {
                if (Equals(value, _resultScreenTime)) return;
                _resultScreenTime = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _raceOverTime;

        public TimeSpan RaceOverTime {
            get => _raceOverTime;
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
            get => _password;
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
            get => _adminPassword;
            set {
                if (Equals(value, _adminPassword)) return;
                _adminPassword = value;
                WrapperPassword = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetAssistState _abs;

        public ServerPresetAssistState Abs {
            get => _abs;
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
            get => _tractionControl;
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
            get => _stabilityControl;
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
            get => _autoClutch;
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
            get => _tyreBlankets;
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
            get => _forceVirtualMirror;
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
            get => _fuelRate;
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
            get => _damageRate;
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
            get => _tyreWearRate;
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
            get => _allowTyresOut;
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
            get => _maxBallast;
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
            get => _qualifyLimitPercentage;
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
            get => _jumpStart;
            set {
                if (Equals(value, _jumpStart)) return;
                _jumpStart = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _raceGasPenaltyDisabled;

        public bool RaceGasPenaltyDisabled {
            get { return _raceGasPenaltyDisabled; }
            set {
                if (Equals(value, _raceGasPenaltyDisabled)) return;
                _raceGasPenaltyDisabled = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Sessions and conditions
        private ChangeableObservableCollection<ServerSessionEntry> _sessions;

        public ChangeableObservableCollection<ServerSessionEntry> Sessions {
            get => _sessions;
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
            get => _driverEntries;
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
            get => _time;
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
            get => $@"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}";
            set {
                int time;
                if (!FlexibleParser.TryParseTime(value, out time)) return;
                Time = time;
            }
        }

        public double SunAngle {
            get => Game.ConditionProperties.GetSunAngle(Time);
            set => Time = Game.ConditionProperties.GetSeconds(value).RoundToInt();
        }

        private bool _dynamicTrackEnabled;

        public bool DynamicTrackEnabled {
            get => _dynamicTrackEnabled;
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
            get => _trackProperties;
            set {
                if (Equals(value, _trackProperties)) return;
                _trackProperties = value;
                OnPropertyChanged();
            }
        }

        private ChangeableObservableCollection<ServerWeatherEntry> _weather;

        public ChangeableObservableCollection<ServerWeatherEntry> Weather {
            get => _weather;
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
            get => _kickVoteQuorum;
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
            get => _sessionVoteQuorum;
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
            get => _voteDuration;
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
            get => _blacklistMode;
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
            get => _maxCollisionsPerKm;
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
    }
}
