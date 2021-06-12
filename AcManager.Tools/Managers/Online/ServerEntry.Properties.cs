using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public enum RaceMode {
        Laps,
        Timed,
        TimedExtra
    }

    public partial class ServerEntry : INotifyDataErrorInfo {
        /// <summary>
        /// Combined from IP and HTTP port.
        /// </summary>
        [NotNull]
        public string Id { get; }

        /// <summary>
        /// IP-address, non-changeable.
        /// </summary>
        public string Ip { get; }

        private bool _isFullyLoaded;

        /// <summary>
        /// In most cases “True”, apart from when ServerEntry was created from a file so only IP and
        /// HTTP port are known.
        /// </summary>
        public bool IsFullyLoaded {
            get => _isFullyLoaded;
            private set => Apply(value, ref _isFullyLoaded);
        }

        private int _portHttp;

        /// <summary>
        /// For json-requests directly to launcher server, non-changeable.
        /// </summary>
        public int PortHttp {
            get => _portHttp;
            private set => Apply(value, ref _portHttp);
        }

        private int _port;

        /// <summary>
        /// As a query argument for //aclobby1.grecian.net/lobby.ashx/….
        /// </summary>
        public int Port {
            get => _port;
            private set => Apply(value, ref _port);
        }

        private int _portRace;

        /// <summary>
        /// For race.ini & acs.exe.
        /// </summary>
        public int PortRace {
            get => _portRace;
            private set => Apply(value, ref _portRace);
        }

        private bool _passwordRequired;

        public bool PasswordRequired {
            get => _passwordRequired;
            set {
                if (Equals(value, _passwordRequired)) return;
                _passwordRequired = value;
                OnPropertyChanged();

                PasswordWasWrong = false;
                InvalidatePasswordIsWrong();
            }
        }

        private string KeyPasswordStorage => $@"__smt_pw_{Id}";

        private string _password;
        private bool _passwordLoaded;

        public string Password {
            get {
                if (!_passwordLoaded) {
                    _passwordLoaded = true;
                    _password = ValuesStorage.GetEncrypted<string>(KeyPasswordStorage);
                }

                return _password;
            }
            set {
                if (Equals(value, Password)) return;
                _password = value;
                ValuesStorage.SetEncrypted(KeyPasswordStorage, value);
                OnPropertyChanged();
                InvalidatePasswordIsWrong();
                PasswordWasWrong = false;
                AvailableUpdate();
                DecryptContentIfNeeded();
            }
        }

        private bool _passwordWasWrong;

        public bool PasswordWasWrong {
            get => _passwordWasWrong;
            set {
                if (Equals(value, _passwordWasWrong)) return;
                _passwordWasWrong = value;
                OnPropertyChanged();
                InvalidatePasswordIsWrong();
            }
        }

        private bool? _correctPassword;

        private void InvalidatePasswordIsWrong() {
            var correct = IsPasswordValid();
            if (_correctPassword != correct) {
                _correctPassword = correct;
                OnPropertyChanged(nameof(PasswordIsWrong));
                OnErrorsChanged(nameof(Password));
            }
        }

        public bool PasswordIsWrong => _correctPassword == false;

        #region Password error thing
        public IEnumerable GetErrors(string propertyName) {
            switch (propertyName) {
                case nameof(Password):
                    return PasswordIsWrong ? new[] { "Password is wrong" } : null;
                default:
                    return null;
            }
        }

        bool INotifyDataErrorInfo.HasErrors => PasswordIsWrong;
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
        #endregion

        private string _country;

        public string Country {
            get => _country;
            set {
                if (value == @"na" || string.IsNullOrWhiteSpace(value)) value = ToolsStrings.Common_NA;
                if (Equals(value, _country)) return;
                _country = value;
                OnPropertyChanged();
            }
        }

        private string _countryId;

        public string CountryId {
            get => _countryId;
            set {
                if (value == @"na") value = "";
                if (Equals(value, _countryId)) return;
                _countryId = value;
                OnPropertyChanged();
            }
        }

        private bool _bookingMode;

        public bool BookingMode {
            get => _bookingMode;
            set {
                if (Equals(value, _bookingMode)) return;
                _bookingMode = value;
                OnPropertyChanged();

                if (!value) {
                    DisposeHelper.Dispose(ref _ui);
                }
            }
        }

        private string[] _cspFeaturesList;

        [CanBeNull]
        public string[] CspFeaturesList {
            get => _cspFeaturesList;
            set => Apply(value, ref _cspFeaturesList, () => OnPropertyChanged(nameof(DisplayCspFeatures)));
        }

        [CanBeNull]
        public string DisplayCspFeatures => CspFeaturesList?.Length > 0 ? CspFeaturesList.JoinToReadableString() : null;

        private int _currentDriversCount;

        public int CurrentDriversCount {
            get => _currentDriversCount;
            set {
                if (Equals(value, _currentDriversCount)) return;
                _currentDriversCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(DisplayClients));
            }
        }

        public bool IsEmpty => CurrentDriversCount == 0;

        private string _time;

        public string Time {
            get => _time;
            set => Apply(value, ref _time);
        }

        private RaceMode _raceMode;

        public RaceMode RaceMode {
            get => _raceMode;
            set => Apply(value, ref _raceMode);
        }

        private DateTime _sessionEnd;

        public DateTime SessionEnd {
            get => _sessionEnd;
            set {
                if (Equals(value, _sessionEnd)) return;
                _sessionEnd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTimeLeft));
            }
        }

        private Game.SessionType? _currentSessionType;

        public Game.SessionType? CurrentSessionType {
            get => _currentSessionType;
            set => Apply(value, ref _currentSessionType);
        }

        public string DisplayTimeLeft {
            get {
                var now = DateTime.Now;
                return RaceMode != RaceMode.Timed && CurrentSessionType == Game.SessionType.Race ? ToolsStrings.Online_Server_SessionInProcess
                        : SessionEnd <= now ? ToolsStrings.Online_Server_SessionEnded : (SessionEnd - now).ToProperString();
            }
        }

        public void OnTick() {
            OnPropertyChanged(nameof(DisplayTimeLeft));
            if (IsBooked && BookingErrorMessage == null) {
                OnPropertyChanged(nameof(BookingTimeLeft));
            }
        }

        public void OnSessionEndTick() {
            OnPropertyChanged(nameof(SessionEnd));
        }

        private int _capacity;

        public int Capacity {
            get => _capacity;
            set {
                if (Equals(value, _capacity)) return;
                _capacity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayClients));
            }
        }

        public string DisplayClients => $@"{CurrentDriversCount}/{Capacity}";

        private long? _ping;

        public long? Ping {
            get => _ping;
            set => Apply(value, ref _ping);
        }

        private bool _isAvailable;

        public bool IsAvailable {
            get => _isAvailable;
            set => Apply(value, ref _isAvailable);
        }

        private int _requiredCspVersion;

        public int RequiredCspVersion {
            get => _requiredCspVersion;
            set => Apply(value, ref _requiredCspVersion);
        }

        private bool _cspRequiredAvailable;

        public bool CspRequiredAvailable {
            get => _cspRequiredAvailable;
            set => Apply(value, ref _cspRequiredAvailable);
        }

        private bool _cspRequiredMissing;

        public bool CspRequiredMissing {
            get => _cspRequiredMissing;
            set => Apply(value, ref _cspRequiredMissing);
        }

        private string _trackId;

        [CanBeNull]
        public string TrackId {
            get => _trackId;
            set => Apply(value, ref _trackId);
        }

        [CanBeNull]
        private TrackObjectBase _track;

        [CanBeNull]
        public TrackObjectBase Track {
            get => _track;
            set {
                var oldValue = _track;
                Apply(value, ref _track, () => {
                    oldValue?.UnsubscribeWeak(OnContentNameChanged);
                    value?.SubscribeWeak(OnContentNameChanged);
                });
            }
        }

        [CanBeNull]
        private List<CarEntry> _cars;

        [CanBeNull]
        public IReadOnlyList<CarEntry> Cars {
            get => _cars;
            private set {
                if (Equals(value, _cars)) return;

                var oldCars = _cars;
                _cars = value?.ToListIfItIsNot();
                OnPropertyChanged();

                if (oldCars != null) {
                    for (var i = oldCars.Count - 1; i >= 0; i--) {
                        var wrapper = oldCars[i]?.CarWrapper;
                        if (wrapper == null) continue;
                        if (wrapper.IsLoaded) {
                            wrapper.Value.UnsubscribeWeak(OnContentNameChanged);
                        }
                        wrapper.UnsubscribeWeak(OnWrappedValueChanged);
                    }
                }

                if (value != null) {
                    for (var i = value.Count - 1; i >= 0; i--) {
                        var wrapper = value[i]?.CarWrapper;
                        if (wrapper?.IsLoaded == false) {
                            wrapper.SubscribeWeak(OnWrappedValueChanged);
                        }
                    }
                }
            }
        }

        private void OnWrappedValueChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(AcItemWrapper.IsLoaded)) {
                OnPropertyChanged(EventCarLoaded);
            }
        }

        private void OnContentNameChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(AcObjectNew.DisplayName)) {
                OnPropertyChanged(EventContentNameChanged);
            }
        }

        public const string EventCarLoaded = "EventCarLoadedChanged";
        public const string EventContentNameChanged = "EventContentNameChanged";

        private int _connectedDrivers;

        public int ConnectedDrivers {
            get => _connectedDrivers;
            set => Apply(value, ref _connectedDrivers);
        }

        private bool _isBookedForPlayer;

        public bool IsBookedForPlayer {
            get => _isBookedForPlayer;
            set => Apply(value, ref _isBookedForPlayer);
        }

        private IReadOnlyList<CurrentDriver> _currentDrivers;

        [CanBeNull]
        public IReadOnlyList<CurrentDriver> CurrentDrivers {
            get => _currentDrivers;
            private set {
                if (Equals(value, _currentDrivers)) return;
                _currentDrivers = value;
                HasFriends = value?.Any(x => x.Tags.ArrayContains(DriverTag.FriendTag)) == true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DriversTagsString));
            }
        }

        private bool _hasFriends;

        public bool HasFriends {
            get => _hasFriends;
            set {
                if (Equals(value, _hasFriends)) return;
                _hasFriends = value;
                OnPropertyChanged();

                if (value) {
                    HasAnyFriends.Value = true;
                }
            }
        }

        private string _driversTagsString;

        /// <summary>
        /// It’s better not to use this property directly, but instead just listen when it’s changed —
        /// that’s when drivers’ tags has been changed.
        /// </summary>
        public string DriversTagsString => _driversTagsString ??
                (_driversTagsString = CurrentDrivers?.SelectMany(x => x.Tags).Select(x => x.DisplayName).JoinToString(',') ?? "");

        internal void RaiseDriversTagsChanged() {
            OnPropertyChanged(nameof(DriversTagsString));
        }

        private IReadOnlyList<Session> _sessions;

        [CanBeNull]
        public IReadOnlyList<Session> Sessions {
            get => _sessions;
            private set {
                if (Equals(value, _sessions)) return;
                _sessions = value;
                OnPropertyChanged();
                CurrentSessionType = Sessions?.FirstOrDefault(x => x.IsActive)?.Type;
            }
        }

        #region State, progress
        private ServerStatus _status;

        public ServerStatus Status {
            get => _status;
            set {
                if (Equals(value, _status)) return;
                _status = value;
                OnPropertyChanged();

                _addToRecentCommand?.RaiseCanExecuteChanged();

                if (value != ServerStatus.Loading) {
                    HasErrors = value == ServerStatus.Error || value == ServerStatus.Unloaded;
                }
            }
        }

        private bool _errorsReady;
        private IReadOnlyList<string> _errors = new List<string>();

        /// <summary>
        /// Cannot be empty, will be null if there is no errors.
        /// </summary>
        [NotNull]
        public IReadOnlyList<string> Errors {
            get {
                if (!_errorsReady) {
                    _errorsReady = true;
                    _errors = GetErrorsList();
                }
                return _errors;
            }
        }

        private string _errorsString;

        /// <summary>
        /// Errors, already joined to one string, for optimization purposes.
        /// </summary>
        [CanBeNull]
        public string ErrorsString => _errorsString
                ?? (_errorsString = Errors.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => $@"• {x.Trim()}").JoinToString('\n'));

        private bool _hasErrors;

        public bool HasErrors {
            get => _hasErrors;
            private set => Apply(value, ref _hasErrors);
        }

        private AsyncProgressEntry _updateProgress;

        public AsyncProgressEntry UpdateProgress {
            get => _updateProgress;
            set => Apply(value, ref _updateProgress);
        }
        #endregion

        private string KeyLastConnected => $@"{Id}lastConnected";

        private bool _lastConnectedLoaded;
        private DateTime? _lastConnected;

        public DateTime? LastConnected {
            get {
                if (!_lastConnectedLoaded) {
                    _lastConnectedLoaded = true;
                    _lastConnected = StatsStorage.Get<DateTime?>(KeyLastConnected);
                }

                return _lastConnected;
            }
            set {
                if (Equals(value, LastConnected)) return;
                _lastConnected = value;
                if (value.HasValue) {
                    StatsStorage.Set(KeyLastConnected, value.Value);
                } else {
                    StatsStorage.Remove(KeyLastConnected);
                }
                OnPropertyChanged();
            }
        }

        private string KeyCountingStatsFrom => $@"{Id}countingStatsFrom";

        private bool _countingStatsFromLoaded;
        private DateTime? _countingStatsFrom;

        public DateTime? CountingStatsFrom {
            get {
                if (!_countingStatsFromLoaded) {
                    _countingStatsFromLoaded = true;
                    _countingStatsFrom = StatsStorage.Get<DateTime?>(KeyCountingStatsFrom);
                }

                return _countingStatsFrom;
            }
            set {
                if (Equals(value, CountingStatsFrom)) return;
                _countingStatsFrom = value;
                if (value.HasValue) {
                    StatsStorage.Set(KeyCountingStatsFrom, value.Value);
                } else {
                    StatsStorage.Remove(KeyCountingStatsFrom);
                }
                OnPropertyChanged();
            }
        }
    }
}