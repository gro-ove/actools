using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class ServerEntry : AcObjectNew, IComparer {
        public class Session {
            public bool IsActive { get; set; }

            /// <summary>
            /// Seconds.
            /// </summary>
            public long Duration { get; set; }

            public Game.SessionType Type { get; set; }

            public string DisplayType => Type.GetDescription() ?? Type.ToString();

            public string DisplayTypeShort => DisplayType.Substring(0, 1);

            public string DisplayDuration => Type == Game.SessionType.Race ?
                    PluralizingConverter.PluralizeExt((int)Duration, ToolsStrings.Online_Session_LapsDuration) :
                    Duration.ToReadableTime();
        }

        public class CarEntry : Displayable, IWithId {
            private CarSkinObject _availableSkin;

            [NotNull]
            public CarObject CarObject { get; }

            public CarEntry([NotNull] CarObject carObject) {
                CarObject = carObject;
            }

            [CanBeNull]
            public CarSkinObject AvailableSkin {
                get { return _availableSkin; }
                set {
                    if (Equals(value, _availableSkin)) return;
                    _availableSkin = value;
                    OnPropertyChanged();

                    if (Total == 0 && value != null) {
                        CarObject.SelectedSkin = value;
                    }
                }
            }

            private int _total;

            public int Total {
                get { return _total; }
                set {
                    if (value == _total) return;
                    _total = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAvailable));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }

            private int _available;

            public int Available {
                get { return _available; }
                set {
                    if (value == _available) return;
                    _available = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAvailable));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }

            public bool IsAvailable => Total == 0 || Available > 0;

            public override string DisplayName {
                get { return Total == 0 ? CarObject.DisplayName : $"{CarObject.DisplayName} ({Available}/{Total})"; }
                set {}
            }

            protected bool Equals(CarEntry other) {
                return Equals(_availableSkin, other._availableSkin) && CarObject.Equals(other.CarObject) && Total == other.Total && Available == other.Available;
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CarEntry)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = _availableSkin?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ CarObject.GetHashCode();
                    hashCode = (hashCode * 397) ^ Total;
                    hashCode = (hashCode * 397) ^ Available;
                    return hashCode;
                }
            }

            public string Id => CarObject.Id;
        }

        public class CarOrOnlyCarIdEntry {
            [CanBeNull]
            public CarObject CarObject => (CarObject)CarObjectWrapper?.Loaded();

            [CanBeNull]
            public AcItemWrapper CarObjectWrapper { get; }

            public string CarId { get; }

            public bool CarExists => CarObjectWrapper != null;

            public CarOrOnlyCarIdEntry(string carId, AcItemWrapper carObjectWrapper = null) {
                CarId = carId;
                CarObjectWrapper = carObjectWrapper;
            }

            public CarOrOnlyCarIdEntry([NotNull] AcItemWrapper carObjectWrapper) {
                CarObjectWrapper = carObjectWrapper;
                CarId = carObjectWrapper.Value.Id;
            }
        }

        public class CurrentDriver {
            public string Name { get; set; }

            public string Team { get; set; }

            public string CarId { get; set; }

            public string CarSkinId { get; set; }

            private CarObject _car;

            public CarObject Car => _car ?? (_car = CarsManager.Instance.GetById(CarId));

            private CarSkinObject _carSkin;

            public CarSkinObject CarSkin => _carSkin ??
                    (_carSkin = CarSkinId != null ? Car?.GetSkinById(CarSkinId) : Car?.GetFirstSkinOrNull());

            protected bool Equals(CurrentDriver other) {
                return string.Equals(Name, other.Name) && string.Equals(Team, other.Team) && string.Equals(CarId, other.CarId) && string.Equals(CarSkinId, other.CarSkinId);
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CurrentDriver)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = Name?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ (Team?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (CarId?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (CarSkinId?.GetHashCode() ?? 0);
                    return hashCode;
                }
            }
        }

        public string Ip { get; }

        /// <summary>
        /// As a query argument for //aclobby1.grecian.net/lobby.ashx/…
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// For json-requests directly to launcher server
        /// </summary>
        public int PortC { get; }

        /// <summary>
        /// For race.ini & acs.exe
        /// </summary>
        public int PortT { get; }

        public bool IsLan { get; }

        public readonly ServerInformation OriginalInformation;

        public bool IsUnavailable { get; }

        public ServerEntry(IOnlineManager manager, string id, bool enabled) : base(manager, id, enabled) {
            IsUnavailable = true;
        }

        public ServerEntry(IOnlineManager manager, [NotNull] ServerInformation information, bool? forceIsLan = null)
                : base(manager, information.GetUniqueId(), true) {
            if (information == null) throw new ArgumentNullException(nameof(information));

            OriginalInformation = information;

            IsLan = forceIsLan ?? information.IsLan;

            Ip = information.Ip;
            Port = information.Port;
            PortC = information.PortC;
            PortT = information.PortT;

            Ping = null;
            SetSomeProperties(information);
        }

        public override void Load() {
        }

        private void SetSomeProperties(ServerInformation information) {
            PreviousUpdateTime = DateTime.Now;
            Name = Regex.Replace(information.Name.Trim(), @"\s+", " ");

            {
                var country = information.Country.FirstOrDefault() ?? "";
                Country = Country != null && country == @"na" ? Country : country;
            }

            {
                var countryId = information.Country.ElementAtOrDefault(1) ?? "";
                CountryId = CountryId != null && countryId == @"na" ? CountryId : countryId;
            }

            CurrentDriversCount = information.Clients;
            Capacity = information.Capacity;

            PasswordRequired = information.Password;
            if (PasswordRequired) {
                Password = ValuesStorage.GetEncryptedString(PasswordStorageKey);
            }
            
            CarIds = information.CarIds;
            CarsOrTheirIds = CarIds.Select(x => new CarOrOnlyCarIdEntry(x, GetCarWrapper(x))).ToList();
            TrackId = information.TrackId;
            Track = GetTrack(TrackId);

            var errorMessage = "";
            var error = SetMissingCarErrorIfNeeded(ref errorMessage);
            error = SetMissingTrackErrorIfNeeded(ref errorMessage) || error;
            if (error) {
                Status = ServerStatus.Error;
                ErrorMessage = errorMessage;
            }

            var seconds = (int)Game.ConditionProperties.GetSeconds(information.Time);
            Time = $"{seconds / 60 / 60:D2}:{seconds / 60 % 60:D2}";
            SessionEnd = DateTime.Now + TimeSpan.FromSeconds(information.TimeLeft - Math.Round(information.Timestamp / 1000d));

            Sessions = information.SessionTypes.Select((x, i) => new Session {
                IsActive = x == information.Session,
                Duration = information.Durations[i],
                Type = (Game.SessionType)x
            }).ToList();

            BookingMode = !information.PickUp;
        }

        private bool SetMissingCarErrorIfNeeded(ref string errorMessage) {
            var list = CarsOrTheirIds.Where(x => !x.CarExists).Select(x => x.CarId).ToList();
            if (!list.Any()) return false;
            errorMessage += (list.Count == 1
                    ? string.Format(ToolsStrings.Online_Server_CarIsMissing, IdToBb(list[0]))
                    : string.Format(ToolsStrings.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToString(@", "))) + Environment.NewLine;
            return true;
        }

        private bool SetMissingTrackErrorIfNeeded(ref string errorMessage) {
            if (Track != null) return false;
            errorMessage += string.Format(ToolsStrings.Online_Server_TrackIsMissing, IdToBb(TrackId, false)) + Environment.NewLine;
            return true;
        }

        private DateTime _previousUpdateTime;

        public DateTime PreviousUpdateTime {
            get { return _previousUpdateTime; }
            set {
                if (Equals(value, _previousUpdateTime)) return;
                _previousUpdateTime = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Update current entry using new information.
        /// </summary>
        /// <param name="information"></param>
        /// <returns>True if update is possible and was done, false if 
        /// changes require to recreate whole ServerEntry</returns>
        private bool UpdateValuesFrom(ServerInformation information) {
            if (Ip != information.Ip ||
                    Port != information.Port ||
                    PortC != information.PortC ||
                    PortT != information.PortT) return false;
            SetSomeProperties(information);
            return true;
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

        private const string PasswordStorageKeyBase = "__smt_pw";

        private string PasswordStorageKey => $"{PasswordStorageKeyBase}_{Id}";

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

        public CarObject GetById([NotNull] string id) {
            return CarsOrTheirIds.FirstOrDefault(x => string.Equals(x.CarId, id, StringComparison.OrdinalIgnoreCase))?.CarObject;
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
                    HasErrors = value == ServerStatus.Error;
                }
            }
        }

        private bool _hasErrors;

        public bool HasErrors {
            get { return _hasErrors; }
            set {
                if (Equals(value, _hasErrors)) return;
                _hasErrors = value;
                OnPropertyChanged();
            }
        }

        private string _errorMessage;

        public string ErrorMessage {
            get { return _errorMessage; }
            set {
                if (Equals(value, _errorMessage)) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        private int _capacity;

        public int Capacity {
            get { return _capacity; }
            set {
                if (Equals(value, _capacity)) return;
                _capacity = value;
                OnPropertyChanged();
            }
        }

        public string DisplayClients => $"{CurrentDriversCount}/{Capacity}";

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

        private string _field;

        public string Field {
            get { return _field; }
            set {
                if (Equals(value, _field)) return;
                _field = value;
                OnPropertyChanged();
            }
        }

        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                OnPropertyChanged();
            }
        }

        private string[] _carIds;

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

        private static AcItemWrapper GetCarWrapper(string informationId) {
            return CarsManager.Instance.GetWrapperById(informationId);
        }

        private static TrackObjectBase GetTrack(string informationId) {
            return TracksManager.Instance.GetById(informationId) ??
                    (informationId.Contains(@"-") ? TracksManager.Instance.GetLayoutById(informationId.ReplaceLastOccurrence(@"-", @"/")) : null);
        }

        public int Compare(object x, object y) {
            return string.Compare(((CarEntry)x).CarObject.DisplayName, ((CarEntry)y).CarObject.DisplayName, StringComparison.CurrentCulture);
        }

        private static string IdToBb(string id, bool car = true) {
            if (car) return string.Format(ToolsStrings.Online_Server_MissingCarBbCode, id);

            id = Regex.Replace(id, @"-([^-]+)$", "/$1");
            if (!id.Contains(@"/")) id = $"{id}/{id}";
            return string.Format(ToolsStrings.Online_Server_MissingTrackBbCode, id);
        }

        [NotNull]
        public BetterObservableCollection<CurrentDriver> CurrentDrivers { get; } = new BetterObservableCollection<CurrentDriver>();

        private List<Session> _sessions;

        [NotNull]
        public List<Session> Sessions {
            get { return _sessions ?? (_sessions = new List<Session>(0)); }
            set {
                if (Equals(value, _sessions)) return;
                _sessions = value;
                OnPropertyChanged();
                CurrentSessionType = Sessions.FirstOrDefault(x => x.IsActive)?.Type;
                _joinCommand?.RaiseCanExecuteChanged();
            }
        }

        public enum UpdateMode {
            Lite,
            Normal,
            Full
        }

        public Task Update(UpdateMode mode, bool background = false) {
            if (!background) {
                Status = ServerStatus.Loading;
                IsAvailable = false;
            }

            return UpdateInner(mode, background);
        }

        private async Task UpdateInner(UpdateMode mode, bool background = false) {
            var errorMessage = "";

            try {
                if (!background) {
                    CurrentDrivers.Clear();
                    OnPropertyChanged(nameof(CurrentDrivers));

                    Status = ServerStatus.Loading;
                    IsAvailable = false;
                }

                SetMissingTrackErrorIfNeeded(ref errorMessage);
                SetMissingCarErrorIfNeeded(ref errorMessage);
                if (!string.IsNullOrWhiteSpace(errorMessage)) return;

                if (!IsLan && SteamIdHelper.Instance.Value == null) {
                    throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
                }

                if (mode == UpdateMode.Full) {
                    var newInformation = await Task.Run(() => IsLan || SettingsHolder.Online.LoadServerInformationDirectly
                            ? KunosApiProvider.TryToGetInformationDirect(Ip, PortC) : KunosApiProvider.TryToGetInformation(Ip, Port));
                    if (newInformation == null) {
                        errorMessage = ToolsStrings.Online_Server_CannotRefresh;
                        return;
                    } else if (!UpdateValuesFrom(newInformation)) {
                        errorMessage = ToolsStrings.Online_Server_NotImplemented;
                        return;
                    }
                }

                var pair = SettingsHolder.Online.ThreadsPing
                        ? await Task.Run(() => KunosApiProvider.TryToPingServer(Ip, Port, SettingsHolder.Online.PingTimeout))
                        : await KunosApiProvider.TryToPingServerAsync(Ip, Port, SettingsHolder.Online.PingTimeout);
                if (pair != null) {
                    Ping = (long)pair.Item2.TotalMilliseconds;
                } else {
                    Ping = null;
                    errorMessage = ToolsStrings.Online_Server_CannotPing;
                    return;
                }

                var information = await KunosApiProvider.TryToGetCurrentInformationAsync(Ip, PortC);
                if (information == null) {
                    errorMessage = ToolsStrings.Online_Server_Unavailable;
                    return;
                }

                ActualInformation = information;
                if (CurrentDrivers.ReplaceIfDifferBy(from x in information.Cars
                                                     where x.IsConnected
                                                     select new CurrentDriver {
                                                         Name = x.DriverName,
                                                         Team = x.DriverTeam,
                                                         CarId = x.CarId,
                                                         CarSkinId = x.CarSkinId
                                                     })) {
                    OnPropertyChanged(nameof(CurrentDrivers));
                }

                CurrentDriversCount = information.Cars.Count(x => x.IsConnected || !string.IsNullOrEmpty(x.DriverName));

                List<CarObject> carObjects;
                if (CarsOrTheirIds.Select(x => x.CarObjectWrapper).Any(x => x?.IsLoaded == false)) {
                    await Task.Delay(50);
                    carObjects = new List<CarObject>(CarsOrTheirIds.Count);
                    foreach (var carOrOnlyCarIdEntry in CarsOrTheirIds.Select(x => x.CarObjectWrapper).Where(x => x != null)) {
                        var loaded = await carOrOnlyCarIdEntry.LoadedAsync();
                        carObjects.Add((CarObject)loaded);
                    }
                } else {
                    carObjects = (from x in CarsOrTheirIds
                                  where x.CarObjectWrapper != null
                                  select (CarObject)x.CarObjectWrapper.Value).ToList();
                }

                foreach (var carObject in carObjects.Where(carObject => carObjects.Any(x => !x.SkinsManager.IsLoaded))) {
                    await Task.Delay(50);
                    await carObject.SkinsManager.EnsureLoadedAsync();
                }

                List<CarEntry> cars;
                if (BookingMode) {
                    cars = CarsOrTheirIds.Select(x => x.CarObject == null ? null : new CarEntry(x.CarObject) {
                        AvailableSkin = x.CarObject.SelectedSkin
                    }).ToList();
                } else {
                    cars = information.Cars.Where(x => x.IsEntryList)
                                      .GroupBy(x => x.CarId)
                                      .Select(g => {
                                          var x = g.ToList();
                                          var car = carObjects.GetByIdOrDefault(x[0].CarId, StringComparison.OrdinalIgnoreCase);
                                          if (car == null) return null;

                                          var availableSkinId = x.FirstOrDefault(y => y.IsConnected == false)?.CarSkinId;
                                          return new CarEntry(car) {
                                              Total = x.Count,
                                              Available = x.Count(y => !y.IsConnected && y.IsEntryList),
                                              AvailableSkin = availableSkinId == null ? null : availableSkinId == string.Empty
                                                      ? car.GetFirstSkinOrNull() : car.GetSkinById(availableSkinId)
                                          };
                                      }).ToList();
                }

                if (cars.Contains(null)) {
                    errorMessage = ToolsStrings.Online_Server_CarsDoNotMatch;
                    return;
                }

                var changed = true;
                if (Cars == null || CarsView == null) {
                    Cars = new BetterObservableCollection<CarEntry>(cars);
                    CarsView = new ListCollectionView(Cars) { CustomSort = this };
                    CarsView.CurrentChanged += SelectedCarChanged;
                } else {
                    // temporary removing listener to avoid losing selected car
                    CarsView.CurrentChanged -= SelectedCarChanged;
                    if (Cars.ReplaceIfDifferBy(cars)) {
                        OnPropertyChanged(nameof(Cars));
                    } else {
                        changed = false;
                    }

                    CarsView.CurrentChanged += SelectedCarChanged;
                }

                if (changed) {
                    LoadSelectedCar();
                }
            } catch (InformativeException e) {
                errorMessage = $"{e.Message}.";
            } catch (Exception e) {
                errorMessage = string.Format(ToolsStrings.Online_Server_UnhandledError, e.Message);
                Logging.Warning("UpdateInner(): " + e);
            } finally {
                ErrorMessage = errorMessage;
                if (!string.IsNullOrWhiteSpace(errorMessage)) {
                    Status = ServerStatus.Error;
                } else if (Status == ServerStatus.Loading) {
                    Status = ServerStatus.Ready;
                }

                AvailableUpdate();
            }
        }

        private string _nonAvailableReason;

        public string NonAvailableReason {
            get { return _nonAvailableReason; }
            set {
                if (Equals(value, _nonAvailableReason)) return;
                _nonAvailableReason = value;
                OnPropertyChanged();
            }
        }

        private string GetNonAvailableReason() {
            if (Status != ServerStatus.Ready) return "CM isn’t ready";

            var currentItem = CarsView?.CurrentItem as CarEntry;
            if (currentItem == null) return "Car isn’t selected";

            if (PasswordRequired) {
                if (WrongPassword) return "Password is invalid";
                if (string.IsNullOrEmpty(Password)) return "Password is required";
            }

            if (BookingMode) {
                var currentSession = Sessions.FirstOrDefault(x => x.IsActive);
                if (currentSession?.Type != Game.SessionType.Booking) return "Wait for the next booking";
            } else {
                if (!currentItem.IsAvailable) return "Selected car isn’t available";
            }

            return null;
        }

        private void AvailableUpdate() {
            NonAvailableReason = GetNonAvailableReason();
            IsAvailable = NonAvailableReason == null;
        }

        private void LoadSelectedCar() {
            if (Cars == null || CarsView == null) return;

            var selected = LimitedStorage.Get(LimitedSpace.OnlineSelectedCar, Id);
            var firstAvailable = (selected == null ? null : Cars.GetByIdOrDefault(selected)) ?? Cars.FirstOrDefault(x => x.IsAvailable);
            CarsView.MoveCurrentTo(firstAvailable);
        }

        private void SelectedCarChanged(object sender, EventArgs e) {
            var selectedCar = GetSelectedCar();
            LimitedStorage.Set(LimitedSpace.OnlineSelectedCar, Id, selectedCar?.Id);
            AvailableUpdate();
        }

        private ICommandExt _addToRecentCommand;

        public ICommand AddToRecentCommand => _addToRecentCommand ?? (_addToRecentCommand = new DelegateCommand(() => {
            RecentManager.Instance.AddRecentServer(OriginalInformation);
        }, () => Status == ServerStatus.Ready && RecentManager.Instance.GetWrapperById(Id) == null));

        private ICommandExt _joinCommand;

        public ICommand JoinCommand => _joinCommand ?? (_joinCommand = new AsyncCommand<object>(Join,
                o => ReferenceEquals(o, ForceJoin) || IsAvailable));

        private ICommandExt _cancelBookingCommand;

        public ICommand CancelBookingCommand => _cancelBookingCommand ?? (_cancelBookingCommand = new AsyncCommand(CancelBooking, () => IsBooked));

        [CanBeNull]
        private IBookingUi _ui;

        public static readonly object ActualJoin = new object();
        public static readonly object ForceJoin = new object();

        [CanBeNull]
        public CarObject GetSelectedCar() {
            return (CarsView?.CurrentItem as CarEntry)?.CarObject;
        }

        [CanBeNull]
        public CarSkinObject GetSelectedCarSkin() {
            return (CarsView?.CurrentItem as CarEntry)?.AvailableSkin;
        }

        private bool _isBooked;

        public bool IsBooked {
            get { return _isBooked; }
            set {
                if (Equals(value, _isBooked)) return;
                _isBooked = value;
                OnPropertyChanged();
                _cancelBookingCommand?.RaiseCanExecuteChanged();
            }
        }

        private DateTime _startTime;

        public DateTime StartTime {
            get { return _startTime; }
            set {
                if (Equals(value, _startTime)) return;
                _startTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BookingTimeLeft));
            }
        }

        private string _bookingErrorMessage;

        public string BookingErrorMessage {
            get { return _bookingErrorMessage; }
            set {
                if (Equals(value, _bookingErrorMessage)) return;
                _bookingErrorMessage = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan BookingTimeLeft {
            get {
                var result = StartTime - DateTime.Now;
                return result <= TimeSpan.Zero ? TimeSpan.Zero : result;
            }
        }

        private async Task CancelBooking() {
            DisposeHelper.Dispose(ref _ui);

            if (!IsBooked) return;
            IsBooked = false;
            await Task.Run(() => KunosApiProvider.TryToUnbook(Ip, PortC));
        }

        private void PrepareBookingUi() {
            if (_ui == null) {
                _ui = _factory.Create();
                _ui.Show(this);
            }
        }

        private void ProcessBookingResponse(BookingResult response) {
            if (_ui?.CancellationToken.IsCancellationRequested == true) {
                CancelBooking().Forget();
                return;
            }

            if (response == null) {
                BookingErrorMessage = "Cannot get any response";
                return;
            }

            if (response.IsSuccessful) {
                StartTime = DateTime.Now + response.Left;
                BookingErrorMessage = null;
                IsBooked = response.IsSuccessful;
            } else {
                BookingErrorMessage = response.ErrorMessage;
                IsBooked = false;
            }

            _ui?.OnUpdate(response);
        }

        public async Task<bool> RebookSkin() {
            if (!IsBooked || !BookingMode || BookingTimeLeft < TimeSpan.FromSeconds(2)) {
                return false;
            }
            
            var carEntry = CarsView?.CurrentItem as CarEntry;
            if (carEntry == null) return false;

            var carId = carEntry.CarObject.Id;
            var correctId = CarIds.FirstOrDefault(x => string.Equals(x, carId, StringComparison.OrdinalIgnoreCase));

            PrepareBookingUi();

            var result = await Task.Run(() => KunosApiProvider.TryToBook(Ip, PortC, Password, correctId, carEntry.AvailableSkin?.Id,
                    DriverName.GetOnline(), ""));
            if (result?.IsSuccessful != true) return false;

            ProcessBookingResponse(result);
            return true;
        }

        private async Task Join(object o) {
            var carEntry = CarsView?.CurrentItem as CarEntry;
            if (carEntry == null) return;

            var carId = carEntry.CarObject.Id;
            var correctId = CarIds.FirstOrDefault(x => string.Equals(x, carId, StringComparison.OrdinalIgnoreCase));

            if (BookingMode && !ReferenceEquals(o, ActualJoin) && !ReferenceEquals(o, ForceJoin)) {
                if (_factory == null) {
                    Logging.Error("Booking: UI factory is missing");
                    return;
                }

                PrepareBookingUi();
                ProcessBookingResponse(await Task.Run(() => KunosApiProvider.TryToBook(Ip, PortC, Password, correctId, carEntry.AvailableSkin?.Id,
                        DriverName.GetOnline(), "")));
                return;
            }

            DisposeHelper.Dispose(ref _ui);
            IsBooked = false;
            BookingErrorMessage = null;

            var properties = new Game.StartProperties(new Game.BasicProperties {
                CarId = carId,
                CarSkinId = carEntry.AvailableSkin?.Id,
                TrackId = Track?.Id,
                TrackConfigurationId = Track?.LayoutId
            }, null, null, null, new Game.OnlineProperties {
                RequestedCar = correctId,
                ServerIp = Ip,
                ServerName = DisplayName,
                ServerPort = PortT,
                ServerHttpPort = PortC,
                Guid = SteamIdHelper.Instance.Value,
                Password = Password
            });

            await GameWrapper.StartAsync(properties);
            var whatsGoingOn = properties.GetAdditional<AcLogHelper.WhatsGoingOn>();
            WrongPassword = whatsGoingOn?.Type == AcLogHelper.WhatsGoingOnType.OnlineWrongPassword;
            if (whatsGoingOn == null) RecentManager.Instance.AddRecentServer(OriginalInformation);
        }

        private ICommand _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand(() => {
            Update(UpdateMode.Full).Forget();
        }));

        [CanBeNull]
        public static ServerEntry FromAddress(IOnlineManager manager, [NotNull] string address) {
            if (address == null) throw new ArgumentNullException(nameof(address));
            var information = KunosApiProvider.TryToGetInformationDirect(address);
            return information == null ? null : new ServerEntry(manager, information, true);
        }

        private static IAnyFactory<IBookingUi> _factory;

        public static void RegisterFactory(IAnyFactory<IBookingUi> factory) {
            _factory = factory;
        }
    }
}
