using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        #region Data

        #region Common fields
        private string _managerDescription;

        public string ManagerDescription {
            get => _managerDescription;
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _trackId)) return;
                _managerDescription = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _webLink;

        public string WebLink {
            get => _webLink;
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _trackId)) return;
                _webLink = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

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

        public bool CspRequiredActual => CspRequired && RequiredCspVersion != PatchHelper.NonExistentVersion;

        private bool _cspRequired;

        public bool CspRequired {
            get => _cspRequired;
            set => Apply(value, ref _cspRequired, () => {
                if (Loaded) {
                    Changed = true;
                    OnPropertyChanged(nameof(CspRequiredActual));
                }
            });
        }

        private int? _requiredCspVersion;

        public int? RequiredCspVersion {
            get => _requiredCspVersion;
            set => Apply(value, ref _requiredCspVersion, () => {
                if (Loaded) {
                    Changed = true;
                    OnPropertyChanged(nameof(CspRequiredActual));
                }
            });
        }

        private string _cspExtraConfig;

        public string CspExtraConfig {
            get {
                InitializeWelcomeMessage();
                return _cspExtraConfig;
            }
            set => Apply(value, ref _cspExtraConfig, () => WelcomeMessageChanged = true);
        }

        private string[] _carIds = new string[0];

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

                UpdateTyresList();
                RefreshSetupCarsValidity();
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
                value = value.Clamp(1, 120);
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

        private bool _disableChecksums;

        public bool DisableChecksums {
            get => _disableChecksums;
            set {
                if (Equals(value, _disableChecksums)) return;
                _disableChecksums = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private static bool IsLocalMessage(string filename) {
            return filename != null && filename.Contains(@"\presets\") && filename.EndsWith(@"\cm_welcome.txt");
        }

        private IDisposable FromServersDirectory() {
            var d = Environment.CurrentDirectory;
            Environment.CurrentDirectory = ServerPresetsManager.ServerDirectory;

            return new ActionAsDisposable(() => { Environment.CurrentDirectory = d; });
        }

        private bool _welcomeMessageLoaded;
        private static readonly string CspConfigSeparator = "\t".RepeatString(32) + "$CSP0:";

        private static byte[] DecompressZlib(byte[] data) {
            using (var m = new MemoryStream(data))
            using (var d = new ZlibStream(m, CompressionMode.Decompress, CompressionLevel.Level6)) {
                return d.ReadAsBytes();
            }
        }

        private static byte[] CompressZlib(byte[] data) {
            using (var m = new MemoryStream()) {
                using (var d = new ZlibStream(m, CompressionMode.Compress, CompressionLevel.Level6)) {
                    d.WriteBytes(data);
                }
                return m.ToArray();
            }
        }

        [CanBeNull]
        private string BuildWelcomeMessage() {
            if (string.IsNullOrEmpty(WelcomeMessage) && string.IsNullOrWhiteSpace(CspExtraConfig)) {
                return null;
            }
            try {
                return string.IsNullOrWhiteSpace(CspExtraConfig) ? WelcomeMessage
                        : WelcomeMessage + CspConfigSeparator + CompressZlib(Encoding.UTF8.GetBytes(CspExtraConfig)).ToCutBase64();
            } catch (Exception e) {
                Logging.Warning(e);
                return WelcomeMessage;
            }
        }

        private void InitializeWelcomeMessage() {
            if (_welcomeMessageLoaded) return;
            _welcomeMessageLoaded = true;

            try {
                var welcomeMessagePath = WelcomeMessagePath;
                if (!string.IsNullOrWhiteSpace(welcomeMessagePath)) {
                    using (FromServersDirectory()) {
                        if (File.Exists(welcomeMessagePath)) {
                            var welcomeMessage = File.ReadAllText(welcomeMessagePath);
                            var separator = welcomeMessage.IndexOf(CspConfigSeparator, StringComparison.Ordinal);
                            if (separator == -1) {
                                _welcomeMessage = welcomeMessage;
                                _cspExtraConfig = null;
                            } else {
                                _welcomeMessage = welcomeMessage.Substring(0, separator);
                                try {
                                    _cspExtraConfig = Encoding.UTF8.GetString(DecompressZlib(
                                            welcomeMessage.Substring(separator + CspConfigSeparator.Length).FromCutBase64()
                                                    ?? throw new NullReferenceException()));
                                } catch (Exception e) {
                                    _cspExtraConfig = null;
                                    Logging.Warning(e);
                                }
                            }
                            WelcomeMessageMissing = false;
                        } else {
                            _welcomeMessage = null;
                            _cspExtraConfig = null;
                            WelcomeMessageMissing = true;
                        }
                    }
                } else {
                    _welcomeMessage = null;
                    _cspExtraConfig = null;
                    WelcomeMessageMissing = false;
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t open welcome message file", e);
                _welcomeMessage = null;
                _cspExtraConfig = null;
                WelcomeMessageMissing = false;
            }
        }

        private string _welcomeMessage;

        public string WelcomeMessage {
            get {
                InitializeWelcomeMessage();
                return _welcomeMessage;
            }
            set {
                if (Equals(value, _welcomeMessage)) return;
                _welcomeMessage = value;
                OnPropertyChanged();
                WelcomeMessageChanged = true;
            }
        }

        private bool _welcomeMessageChanged;

        public bool WelcomeMessageChanged {
            get => _welcomeMessageChanged;
            set {
                if (Equals(value, _welcomeMessageChanged)) return;
                _welcomeMessageChanged = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private DelegateCommand _saveWelcomeMessageCommand;

        public DelegateCommand SaveWelcomeMessageCommand => _saveWelcomeMessageCommand ?? (_saveWelcomeMessageCommand = new DelegateCommand(() => {
            using (FromServersDirectory()) {
                if (_welcomeMessagePath == null || !IsLocalMessage(_welcomeMessagePath) && !Directory.Exists(Path.GetDirectoryName(_welcomeMessagePath))) {
                    _welcomeMessagePath = $@"{Location}\cm_welcome.txt";
                    OnPropertyChanged(nameof(WelcomeMessagePath));
                }

                try {
                    File.WriteAllText(_welcomeMessagePath, BuildWelcomeMessage() ?? "");
                    WelcomeMessageChanged = false;
                    WelcomeMessageMissing = false;
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t save welcome message file", e);
                }
            }
        }, () => _welcomeMessageChanged));

        private string _welcomeMessagePath;

        [CanBeNull]
        public string WelcomeMessagePath {
            get => _welcomeMessagePath;
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (IsLocalMessage(value)) {
                    value = $@"{Location}\cm_welcome.txt";
                }

                if (Equals(value, _welcomeMessagePath)) return;
                _welcomeMessagePath = value;
                OnPropertyChanged();

                _welcomeMessageLoaded = false;
                OnPropertyChanged(nameof(WelcomeMessage));
                OnPropertyChanged(nameof(CspExtraConfig));

                WelcomeMessageChanged = false;
                Changed = true;
            }
        }

        private bool _welcomeMessageMissing;

        public bool WelcomeMessageMissing {
            get => _welcomeMessageMissing;
            set => Apply(value, ref _welcomeMessageMissing);
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
                value = value.Clamp(-1, 4);
                if (Equals(value, _allowTyresOut)) return;
                _allowTyresOut = value;
                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayAllowTyresOut));
                    Changed = true;
                }
            }
        }

        public string DisplayAllowTyresOut {
            get => AllowTyresOut < 0 ? "Any" : AllowTyresOut.ToInvariantString();
            set => AllowTyresOut = value?.As(-1) ?? -1;
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
            get => _raceGasPenaltyDisabled;
            set {
                if (Equals(value, _raceGasPenaltyDisabled)) return;
                _raceGasPenaltyDisabled = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion

        #region Sessions and conditions
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
                    UpdateIndexes();
                }
            }
        }

        private void UpdateIndexes() {
            if (_driverEntries == null) return;
            for (var i = 0; i < _driverEntries.Count; i++) {
                _driverEntries[i].Index = i + 1;
            }
        }

        private void UpdateCarIds() {
            CarIds = _driverEntries.Select(x => x.CarId).Distinct().ToArray();
        }

        public event NotifyCollectionChangedEventHandler DriverCollectionChanged;
        public event PropertyChangedEventHandler DriverPropertyChanged;

        private void OnDriverEntriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateCarIds();
            UpdateIndexes();

            if (Loaded) {
                Changed = true;
                DriverCollectionChanged?.Invoke(sender, e);
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
                case nameof(ServerPresetDriverEntry.Cloned):
                    var en = (ServerPresetDriverEntry)sender;
                    if (en.Cloned) {
                        en.Cloned = false;
                        DriverEntries.Insert(DriverEntries.IndexOf(en) + 1, en.Clone());
                    }
                    return;
            }

            if (Loaded) {
                Changed = true;
                DriverPropertyChanged?.Invoke(sender, e);
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

        private double _timeMultiplier;

        public double TimeMultiplier {
            get => _timeMultiplier;
            set {
                value = value.Clamp(0, 3600);
                if (Equals(value, _timeMultiplier)) return;
                _timeMultiplier = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        public string DisplayTime {
            get => _time.ToDisplayTime();
            set {
                if (!FlexibleParser.TryParseTime(value, out var time)) return;
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
            set => Apply(value, ref _trackProperties);
        }

        private ChangeableObservableCollection<ServerWeatherEntry> _weather;

        [CanBeNull]
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

        public event NotifyCollectionChangedEventHandler WeatherCollectionChanged;
        public event PropertyChangedEventHandler WeatherEntryPropertyChanged;

        private void OnWeatherCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateWeatherIndexes();
            if (Loaded) {
                Changed = true;
                WeatherCollectionChanged?.Invoke(sender, e);
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
                    Weather?.Remove((ServerWeatherEntry)sender);
                    return;
            }

            if (Loaded) {
                Changed = true;
                WeatherEntryPropertyChanged?.Invoke(sender, e);
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

        #region Plugins
        private bool _useCmPlugin;

        public bool UseCmPlugin {
            get => _useCmPlugin;
            set => Apply(value, ref _useCmPlugin, () => {
                if (Loaded) Changed = true;
            });
        }

        public class PluginEntry : NotifyPropertyChanged {
            private string _address;

            public string Address {
                get => _address;
                set => Apply(value, ref _address);
            }

            private int? _udpPort;

            public int? UdpPort {
                get => _udpPort;
                set {
                    if (value == 0) value = null;
                    Apply(value, ref _udpPort);
                }
            }
        }

        private int? _pluginUdpPort;

        public int? PluginUdpPort {
            get => _pluginUdpPort;
            set {
                if (value == 0) value = null;
                if (Equals(value, _pluginUdpPort)) return;
                _pluginUdpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _pluginUdpAddress;

        public string PluginUdpAddress {
            get => _pluginUdpAddress;
            set {
                if (Equals(value, _pluginUdpAddress)) return;
                _pluginUdpAddress = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _pluginAuthAddress;

        public string PluginAuthAddress {
            get => _pluginAuthAddress;
            set {
                if (Equals(value, _pluginAuthAddress)) return;
                _pluginAuthAddress = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion

        #region Advanced
        private string _ftpHost;

        public string FtpHost {
            get => _ftpHost;
            set {
                if (Equals(value, _ftpHost)) return;
                _ftpHost = value;
                if (Loaded) {
                    OnPropertyChanged();
                    _ftpVerifyConnectionCommand?.RaiseCanExecuteChanged();
                    _ftpUploadContentCommand?.RaiseCanExecuteChanged();
                    Changed = true;
                }
            }
        }

        private string _ftpLogin;

        public string FtpLogin {
            get => _ftpLogin;
            set {
                if (Equals(value, _ftpLogin)) return;
                _ftpLogin = value;
                if (Loaded) {
                    OnPropertyChanged();
                    _ftpVerifyConnectionCommand?.RaiseCanExecuteChanged();
                    _ftpUploadContentCommand?.RaiseCanExecuteChanged();
                    Changed = true;
                }
            }
        }

        private string _ftpPassword;

        public string FtpPassword {
            get => _ftpPassword;
            set {
                if (Equals(value, _ftpPassword)) return;
                _ftpPassword = value;
                if (Loaded) {
                    OnPropertyChanged();
                    _ftpVerifyConnectionCommand?.RaiseCanExecuteChanged();
                    _ftpUploadContentCommand?.RaiseCanExecuteChanged();
                    Changed = true;
                }
            }
        }

        private string _ftpDirectory;

        public string FtpDirectory {
            get => _ftpDirectory;
            set {
                if (Equals(value, _ftpDirectory)) return;
                _ftpDirectory = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _ftpClearBeforeUpload;

        public bool FtpClearBeforeUpload {
            get => _ftpClearBeforeUpload;
            set {
                if (Equals(value, _ftpClearBeforeUpload)) return;
                _ftpClearBeforeUpload = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _ftpUploadDataOnly;

        public bool FtpUploadDataOnly {
            get => _ftpUploadDataOnly;
            set {
                if (Equals(value, _ftpUploadDataOnly)) return;
                _ftpUploadDataOnly = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetPackMode _ftpMode;

        public ServerPresetPackMode FtpMode {
            get => _ftpMode;
            set {
                if (Equals(value, _ftpMode)) return;
                _ftpMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion
    }
}