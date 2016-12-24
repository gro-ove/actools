using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
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
            get { return _isFullyLoaded; }
            private set {
                if (Equals(value, _isFullyLoaded)) return;
                _isFullyLoaded = value;
                OnPropertyChanged();
            }
        }

        private int _portHttp;

        /// <summary>
        /// For json-requests directly to launcher server, non-changeable.
        /// </summary>
        public int PortHttp {
            get { return _portHttp; }
            private set {
                if (Equals(value, _portHttp)) return;
                _portHttp = value;
                OnPropertyChanged();
            }
        }

        private int _port;

        /// <summary>
        /// As a query argument for //aclobby1.grecian.net/lobby.ashx/….
        /// </summary>
        public int Port {
            get { return _port; }
            private set {
                if (Equals(value, _port)) return;
                _port = value;
                OnPropertyChanged();
            }
        }

        private int _portRace;

        /// <summary>
        /// For race.ini & acs.exe.
        /// </summary>
        public int PortRace {
            get { return _portRace; }
            private set {
                if (Equals(value, _portRace)) return;
                _portRace = value;
                OnPropertyChanged();
            }
        }

        private bool _passwordRequired;

        public bool PasswordRequired {
            get { return _passwordRequired; }
            set {
                if (Equals(value, _passwordRequired)) return;
                _passwordRequired = value;
                OnPropertyChanged();

                _wrongPassword = false;
                OnPropertyChanged(nameof(WrongPassword));
                _joinCommand?.RaiseCanExecuteChanged();
            }
        }

        private string KeyPasswordStorage => $@"__smt_pw_{Id}";

        private string _password;

        public string Password {
            get { return _password; }
            set {
                if (Equals(value, _password)) return;
                _password = value;
                ValuesStorage.SetEncrypted(KeyPasswordStorage, value);
                OnPropertyChanged();

                _wrongPassword = false;
                OnPropertyChanged(nameof(WrongPassword));
                _joinCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _wrongPassword;

        public bool WrongPassword {
            get { return _wrongPassword; }
            set {
                if (Equals(value, _wrongPassword)) return;
                _wrongPassword = value;
                OnPropertyChanged();
                _joinCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _country;

        public string Country {
            get { return _country; }
            set {
                if (value == @"na" || string.IsNullOrWhiteSpace(value)) value = ToolsStrings.Common_NA;
                if (Equals(value, _country)) return;
                _country = value;
                OnPropertyChanged();
            }
        }

        private string _countryId;

        public string CountryId {
            get { return _countryId; }
            set {
                if (value == @"na") value = "";
                if (Equals(value, _countryId)) return;
                _countryId = value;
                OnPropertyChanged();
            }
        }

        private bool _bookingMode;

        public bool BookingMode {
            get { return _bookingMode; }
            set {
                if (Equals(value, _bookingMode)) return;
                _bookingMode = value;
                OnPropertyChanged();
                _joinCommand?.RaiseCanExecuteChanged();

                if (!value) {
                    DisposeHelper.Dispose(ref _ui);
                }
            }
        }

        private int _currentDriversCount;

        public int CurrentDriversCount {
            get { return _currentDriversCount; }
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
            get { return _time; }
            set {
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
            }
        }

        private DateTime _sessionEnd;

        public DateTime SessionEnd {
            get { return _sessionEnd; }
            set {
                if (Equals(value, _sessionEnd)) return;
                _sessionEnd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTimeLeft));
            }
        }

        private Game.SessionType? _currentSessionType;

        public Game.SessionType? CurrentSessionType {
            get { return _currentSessionType; }
            set {
                if (Equals(value, _currentSessionType)) return;
                _currentSessionType = value;
                OnPropertyChanged();
            }
        }

        public string DisplayTimeLeft {
            get {
                var now = DateTime.Now;
                return CurrentSessionType == Game.SessionType.Race ? ToolsStrings.Online_Server_SessionInProcess
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
            get { return _capacity; }
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
            get { return _ping; }
            set {
                if (Equals(value, _ping)) return;
                _ping = value;
                OnPropertyChanged();
            }
        }

        private bool _isAvailable;

        public bool IsAvailable {
            get { return _isAvailable; }
            set {
                if (Equals(value, _isAvailable)) return;
                _isAvailable = value;
                OnPropertyChanged();
                _joinCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _trackId;

        [CanBeNull]
        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        private TrackObjectBase _track;

        [CanBeNull]
        public TrackObjectBase Track {
            get { return _track; }
            set {
                if (Equals(value, _track)) return;
                _track = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        private List<CarEntry> _cars;

        [CanBeNull]
        public IReadOnlyList<CarEntry> Cars {
            get { return _cars; }
            private set {
                if (Equals(value, _cars)) return;
                _cars = value?.ToListIfItsNot();
                OnPropertyChanged();
            }
        }

        private int _connectedDrivers;

        public int ConnectedDrivers {
            get { return _connectedDrivers; }
            set {
                if (Equals(value, _connectedDrivers)) return;
                _connectedDrivers = value;
                OnPropertyChanged();
            }
        }

        private bool _isBookedForPlayer;

        public bool IsBookedForPlayer {
            get { return _isBookedForPlayer; }
            set {
                if (Equals(value, _isBookedForPlayer)) return;
                _isBookedForPlayer = value;
                OnPropertyChanged();
            }
        }

        private IReadOnlyList<CurrentDriver> _currentDrivers;

        [CanBeNull]
        public IReadOnlyList<CurrentDriver> CurrentDrivers {
            get { return _currentDrivers; }
            private set {
                if (Equals(value, _currentDrivers)) return;
                _currentDrivers = value;
                HasFriends = value?.Any(x => x.Tags.Contains(DriverTag.FriendTag)) == true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DriversTagsString));
            }
        }

        private bool _hasFriends;

        public bool HasFriends {
            get { return _hasFriends; }
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
            get { return _sessions; }
            private set {
                if (Equals(value, _sessions)) return;
                _sessions = value;
                OnPropertyChanged();
                CurrentSessionType = Sessions?.FirstOrDefault(x => x.IsActive)?.Type;
                _joinCommand?.RaiseCanExecuteChanged();
            }
        }

        #region State, progress
        private ServerStatus _status;

        public ServerStatus Status {
            get { return _status; }
            set {
                if (Equals(value, _status)) return;
                _status = value;
                OnPropertyChanged();

                _joinCommand?.RaiseCanExecuteChanged();
                _addToRecentCommand?.RaiseCanExecuteChanged();

                if (value != ServerStatus.Loading) {
                    HasErrors = value == ServerStatus.Error || value == ServerStatus.Unloaded;
                }
            }
        }

        private IReadOnlyList<string> _errors;

        /// <summary>
        /// Cannot be empty, will be null if there is no errors.
        /// </summary>
        [CanBeNull]
        public IReadOnlyList<string> Errors {
            get { return _errors; }
            set {
                if (value != null && value.Count == 0) value = null;
                if (Equals(value, _errors)) return;
                _errors = value;
                _errorsString = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ErrorsString));
            }
        }
        
        /// <summary>
        /// Errors, already joined to one string, for optimization purposes.
        /// </summary>
        [CanBeNull]
        public string ErrorsString => _errors == null ? null : (_errorsString ?? (_errorsString = _errors.JoinToString('\n')));
        private string _errorsString;

        private bool _hasErrors;
        
        public bool HasErrors {
            get { return _hasErrors; }
            private set {
                if (Equals(value, _hasErrors)) return;
                _hasErrors = value;
                OnPropertyChanged();
            }
        }

        private AsyncProgressEntry _updateProgress;

        public AsyncProgressEntry UpdateProgress {
            get { return _updateProgress; }
            set {
                if (Equals(value, _updateProgress)) return;
                _updateProgress = value;
                OnPropertyChanged();
            }
        }
        #endregion

        private string KeyLastConnected => $@"{Id}lastConnected";

        private bool _lastConnectedLoaded;
        private DateTime? _lastConnected;

        public DateTime? LastConnected {
            get {
                if (!_lastConnectedLoaded) {
                    _lastConnectedLoaded = true;
                    _lastConnected = StatsStorage.GetDateTime(KeyLastConnected);
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
    }
}
