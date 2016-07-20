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
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Lists;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
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
                    PluralizingConverter.PluralizeExt((int)Duration, Resources.Online_Session_LapsDuration) :
                    Duration.ToReadableTime();
        }

        public class CarEntry {
            [NotNull]
            public CarObject CarObject { get; }

            public CarEntry([NotNull] CarObject carObject) {
                CarObject = carObject;
            }

            public CarSkinObject AvailableSkin { get; set; }

            public int Total { get; set; }

            public int Available { get; set; }

            public bool IsAvailable => Available > 0;

            public override string ToString() {
                return $"{CarObject.DisplayName} ({Available}/{Total})";
            }
        }

        public class CarOrOnlyCarIdEntry {
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

        public ServerEntry(IOnlineManager manager, [NotNull] ServerInformation information) : base(manager, information.GetUniqueId(), true) {
            if (information == null) throw new ArgumentNullException(nameof(information));

            OriginalInformation = information;
            
            IsLan = information.IsLan;

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
            
            SetMissingCarErrorIfNeeded();
            SetMissingTrackErrorIfNeeded();

            var seconds = (int)Game.ConditionProperties.GetSeconds(information.Time);
            Time = $"{seconds / 60 / 60:D2}:{seconds / 60 % 60:D2}";
            SessionEnd = DateTime.Now + TimeSpan.FromSeconds(information.TimeLeft - Math.Round(information.Timestamp / 1000d));

            Sessions = information.SessionTypes.Select((x, i) => new Session {
                IsActive = x == information.Session,
                Duration = information.Durations[i],
                Type = (Game.SessionType)x
            }).ToList();
        }

        private void SetMissingCarErrorIfNeeded() {
            var list = CarsOrTheirIds.Where(x => !x.CarExists).Select(x => x.CarId).ToList();
            if (!list.Any()) return;
            Status = ServerStatus.Error;
            ErrorMessage += (list.Count == 1
                    ? string.Format(Resources.Online_Server_CarIsMissing, IdToBb(list[0]))
                    : string.Format(Resources.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToString(", "))) + "\n";
        }

        private void SetMissingTrackErrorIfNeeded() {
            if (Track != null) return;
            Status = ServerStatus.Error;
            ErrorMessage += string.Format(Resources.Online_Server_TrackIsMissing, IdToBb(TrackId, false)) + "\n";
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

                JoinCommand.OnCanExecuteChanged();
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

                JoinCommand.OnCanExecuteChanged();
            }
        }

        private bool _wrongPassword;

        public bool WrongPassword {
            get { return _wrongPassword; }
            set {
                if (Equals(value, _wrongPassword)) return;
                _wrongPassword = value;
                OnPropertyChanged();
                JoinCommand.OnCanExecuteChanged();
            }
        }

        private string _country;
        
        public string Country {
            get { return _country; }
            set {
                if (value == @"na") value = Resources.Online_Server_CountryNA;
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

        public string DisplayTimeLeft {
            get {
                var now = DateTime.Now;
                return SessionEnd <= now ? Resources.Online_Server_SessionEnded : (SessionEnd - now).ToProperString();
            }
        }

        public void OnTick() {
            OnPropertyChanged(nameof(DisplayTimeLeft));
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
                JoinCommand.OnCanExecuteChanged();
                AddToRecentCommand.OnCanExecuteChanged();

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
                JoinCommand.OnCanExecuteChanged();
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
        private TrackBaseObject _track;

        [CanBeNull]
        public TrackBaseObject Track {
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

        private static TrackBaseObject GetTrack(string informationId) {
            return TracksManager.Instance.GetById(informationId) ??
                    (informationId.Contains("-") ? TracksManager.Instance.GetLayoutById(informationId.ReplaceLastOccurrence("-", "/")) : null);
        }

        public int Compare(object x, object y) {
            return string.Compare(((CarEntry)x).CarObject.DisplayName, ((CarEntry)y).CarObject.DisplayName, StringComparison.CurrentCulture);
        }

        private static string IdToBb(string id, bool car = true) {
            if (car) return string.Format(Resources.Online_Server_MissingCarBbCode, id);

            id = Regex.Replace(id, @"-([^-]+)$", "/$1");
            if (!id.Contains("/")) id = $"{id}/{id}";
            return string.Format(Resources.Online_Server_MissingTrackBbCode, id);
        }

        private BetterObservableCollection<CurrentDriver> _currentDrivers;

        [CanBeNull]
        public BetterObservableCollection<CurrentDriver> CurrentDrivers {
            get { return _currentDrivers; }
            set {
                if (Equals(value, _currentDrivers)) return;
                _currentDrivers = value;
                OnPropertyChanged();
            }
        }

        private List<Session> _sessions;

        public List<Session> Sessions {
            get { return _sessions; }
            set {
                if (Equals(value, _sessions)) return;
                _sessions = value;
                OnPropertyChanged();
            }
        }

        public enum UpdateMode {
            Lite, Normal, Full
        }

        public async Task Update(UpdateMode mode) {
            try {
                if (CurrentDrivers == null) {
                    CurrentDrivers = new BetterObservableCollection<CurrentDriver>();
                }

                Status = ServerStatus.Loading;
                ErrorMessage = "";
                IsAvailable = false;

                CurrentDrivers.Clear();
                OnPropertyChanged(nameof(CurrentDrivers));

                if (mode == UpdateMode.Full) {
                    var newInformation = await Task.Run(() => IsLan || SettingsHolder.Online.LoadServerInformationDirectly ?
                            KunosApiProvider.TryToGetInformationDirect(Ip, PortC) :
                            KunosApiProvider.TryToGetInformation(Ip, Port));
                    if (newInformation == null) {
                        Status = ServerStatus.Error;
                        ErrorMessage += Resources.Online_Server_CannotRefresh;
                    } else {
                        // TODO
                        if (!UpdateValuesFrom(newInformation)) {
                            Status = ServerStatus.Error;
                            ErrorMessage += Resources.Online_Server_NotImplemented;
                        }
                    }
                } else {
                    SetMissingTrackErrorIfNeeded();
                    SetMissingCarErrorIfNeeded();
                }

                if (Status == ServerStatus.Error) {
                    return;
                }

                var pair = SettingsHolder.Online.ThreadsPing
                        ? await Task.Run(() => KunosApiProvider.TryToPingServer(Ip, Port, SettingsHolder.Online.PingTimeout))
                        : await KunosApiProvider.TryToPingServerAsync(Ip, Port, SettingsHolder.Online.PingTimeout);
                if (pair != null) {
                    Ping = (long)pair.Item2.TotalMilliseconds;
                } else {
                    Ping = null;
                    Status = ServerStatus.Error;
                    ErrorMessage += Resources.Online_Server_CannotPing;
                    return;
                }

                var information = await KunosApiProvider.TryToGetCurrentInformationAsync(Ip, PortC);
                if (information == null) {
                    Status = ServerStatus.Error;
                    ErrorMessage = Resources.Online_Server_Unavailable;
                    return;
                }

                ActualInformation = information;
                CurrentDrivers.ReplaceEverythingBy(from x in information.Cars
                                                   where x.IsConnected
                                                   select new CurrentDriver {
                                                       Name = x.DriverName,
                                                       Team = x.DriverTeam,
                                                       CarId = x.CarId,
                                                       CarSkinId = x.CarSkinId
                                                   });
                OnPropertyChanged(nameof(CurrentDrivers));

                CurrentDriversCount = CurrentDrivers.Count;

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

                var cars = (from x in information.Cars
                            where x.IsEntryList // if IsEntryList means that car could be selected
                            group x by x.CarId
                            into g
                            let list = g.ToList()
                            let carObject = carObjects.GetByIdOrDefault(list[0].CarId, StringComparison.OrdinalIgnoreCase)
                            let availableSkinId = list.FirstOrDefault(y => y.IsConnected == false)?.CarSkinId
                            select carObject == null ? null : new CarEntry(carObject) {
                                Total = list.Count,
                                Available = list.Count(y => !y.IsConnected && y.IsEntryList),
                                AvailableSkin = availableSkinId == null ? null :
                                        availableSkinId == string.Empty ? carObject.GetFirstSkinOrNull() : carObject.GetSkinById(availableSkinId)
                            }).ToList();

                if (cars.Contains(null)) {
                    Status = ServerStatus.Error;
                    ErrorMessage = Resources.Online_Server_CarsDoNotMatch;
                    return;
                }

                if (Cars == null || CarsView == null) {
                    Cars = new BetterObservableCollection<CarEntry>(cars);
                    CarsView = new ListCollectionView(Cars) { CustomSort = this };
                } else {
                    Cars.ReplaceEverythingBy(cars);
                    OnPropertyChanged(nameof(Cars));
                }

                var firstAvailable = Cars.FirstOrDefault(x => x.IsAvailable);
                CarsView.MoveCurrentTo(firstAvailable);
                IsAvailable = firstAvailable != null;

                Status = ServerStatus.Ready;
            } catch (Exception e) {
                Status = ServerStatus.Error;
                ErrorMessage = Resources.Online_Server_UnhandledError;
                Logging.Warning("ServerEntry error:" + e);
            }
        }

        private RelayCommand _addToRecentCommand;

        public RelayCommand AddToRecentCommand => _addToRecentCommand ?? (_addToRecentCommand = new RelayCommand(o => {
            RecentManager.Instance.AddRecentServer(OriginalInformation);
        }, o => Status == ServerStatus.Ready && RecentManager.Instance.GetWrapperById(Id) == null));

        private AsyncCommand _joinCommand;

        public AsyncCommand JoinCommand => _joinCommand ?? (_joinCommand = new AsyncCommand(Join, JoinAvailable));

        private async Task Join(object o) {
            if (CarsView == null) return;

            var carId = (CarsView.CurrentItem as CarEntry)?.CarObject.Id;
            var correctId = CarIds.FirstOrDefault(x => string.Equals(x, carId, StringComparison.OrdinalIgnoreCase));
            var properties = new Game.StartProperties(new Game.BasicProperties {
                CarId = carId,
                CarSkinId = null,
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
            var whatsGoingOn = properties.GetAdditional<AcLogHelper.WhatsGoingOn?>();
            WrongPassword = whatsGoingOn == AcLogHelper.WhatsGoingOn.OnlineWrongPassword;
            if (whatsGoingOn == null) RecentManager.Instance.AddRecentServer(OriginalInformation);
        }

        private bool JoinAvailable(object o) {
            return IsAvailable && (!PasswordRequired || !WrongPassword && !string.IsNullOrEmpty(Password)) && Status == ServerStatus.Ready;
        }

        private ICommand _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new RelayCommand(o => {
            Update(UpdateMode.Full).Forget();
        }));

        [CanBeNull]
        public static ServerEntry FromAddress(IOnlineManager manager, [NotNull] string address) {
            if (address == null) throw new ArgumentNullException(nameof(address));
            var information = KunosApiProvider.TryToGetInformationDirect(address);
            return information == null ? null : new ServerEntry(manager, information);
        }
    }
}
