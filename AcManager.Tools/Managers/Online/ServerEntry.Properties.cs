using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {

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

        private const string PasswordStorageKeyBase = "__smt_pw";

        private string PasswordStorageKey => $@"{PasswordStorageKeyBase}_{Id}";

        private string _password;

        public string Password {
            get { return _password; }
            set {
                if (Equals(value, _password)) return;
                _password = value;
                ValuesStorage.SetEncrypted(PasswordStorageKey, value);
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
                if (value == @"na") value = ToolsStrings.Common_NA;
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

        private ServerActualInformation _actualInformation;

        public ServerActualInformation ActualInformation {
            get { return _actualInformation; }
            set {
                if (Equals(value, _actualInformation)) return;
                _actualInformation = value;
                OnPropertyChanged();
            }
        }

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

        private string[] _carIds;

        [CanBeNull]
        public string[] CarIds {
            get { return _carIds; }
            set {
                if (Equals(value, _carIds)) return;
                _carIds = value;
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

        private List<CarOrOnlyCarIdEntry> _carsOrTheirIds;

        [CanBeNull]
        public List<CarOrOnlyCarIdEntry> CarsOrTheirIds {
            get { return _carsOrTheirIds; }
            set {
                if (Equals(value, _carsOrTheirIds)) return;
                _carsOrTheirIds = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        private BetterObservableCollection<CarEntry> _cars;

        [CanBeNull]
        public BetterObservableCollection<CarEntry> Cars {
            get { return _cars; }
            set {
                if (Equals(value, _cars)) return;
                _cars = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        private ListCollectionView _carsView;

        [CanBeNull]
        public ListCollectionView CarsView {
            get { return _carsView; }
            set {
                if (Equals(value, _carsView)) return;
                _carsView = value;
                OnPropertyChanged();
            }
        }

        #region State, progress
        private ServerStatus _status;

        public ServerStatus Status {
            get { return _status; }
            set {
                if (Equals(value, _status))
                    return;
                _status = value;
                OnPropertyChanged();

                _joinCommand?.RaiseCanExecuteChanged();
                _addToRecentCommand?.RaiseCanExecuteChanged();

                if (value != ServerStatus.Loading) {
                    HasErrors = value == ServerStatus.Error;
                }
            }
        }

        private string _errorMessage;

        [CanBeNull]
        public string ErrorMessage {
            get { return _errorMessage; }
            set {
                if (Equals(value, _errorMessage))
                    return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _hasErrors;

        public bool HasErrors {
            get { return _hasErrors; }
            set {
                if (Equals(value, _hasErrors))
                    return;
                _hasErrors = value;
                OnPropertyChanged();
            }
        }

        private double _updateProgress;

        public double UpdateProgress {
            get { return _updateProgress; }
            set {
                if (Equals(value, _updateProgress))
                    return;
                _updateProgress = value;
                OnPropertyChanged();
            }
        }

        private string _updateProgressMessage;

        public string UpdateProgressMessage {
            get { return _updateProgressMessage; }
            set {
                if (Equals(value, _updateProgressMessage))
                    return;
                _updateProgressMessage = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
