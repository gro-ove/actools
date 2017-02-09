using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry : Displayable, IWithId {
        public ServerEntry([NotNull] ServerInformation information) {
            if (information == null) throw new ArgumentNullException(nameof(information));

            Id = information.Id;
            Ip = information.Ip;
            PortHttp = information.PortHttp;

            Ping = null;

            var errors = new List<string>(3);
            var status = UpdateValues(information, errors, true);
            Status = status ?? ServerStatus.Unloaded;
            Errors = errors;
        }

        public void UpdateValues([NotNull] ServerInformation information) {
            var errors = new List<string>(3);
            var status = UpdateValues(information, errors, true);
            if (status.HasValue) {
                Status = status.Value;
                Errors = errors;
            }
        }

        private string _actualName;

        public string ActualName {
            get { return _actualName; }
            set {
                if (Equals(value, _actualName)) return;
                _actualName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Sets properties based on loaded information.
        /// </summary>
        /// <param name="baseInformation">Loaded information.</param>
        /// <param name="errors">Errors will be put here.</param>
        /// <param name="setCurrentDriversCount">Set CurrentDriversCount property.</param>
        /// <returns>Null if everything is OK, ServerStatus.Error/ServerStatus.Unloaded message otherwise.</returns>
        private ServerStatus? UpdateValues([NotNull] ServerInformation baseInformation, [NotNull] ICollection<string> errors, bool setCurrentDriversCount) {
            if (Ip != baseInformation.Ip) {
                errors.Add($"IP changed (from {Ip} to {baseInformation.Ip})");
                return ServerStatus.Error;
            }

            if (PortHttp != baseInformation.PortHttp) {
                errors.Add($"HTTP port changed (from {PortHttp} to {baseInformation.PortHttp})");
                return ServerStatus.Error;
            }


            var information = baseInformation as ServerInformationComplete;

            if (!IsFullyLoaded || information != null) {
                DisplayName = baseInformation.Name == null ? Id : CleanUp(baseInformation.Name, DisplayName);
                ActualName = baseInformation.Name ?? Id;
            }

            if (information == null) {
                return null;
            }

            IsFullyLoaded = true;
            Port = information.Port;
            PortRace = information.PortRace;
            PreviousUpdateTime = DateTime.Now;

            {
                var country = information.Country?.FirstOrDefault() ?? "";
                Country = Country != null && country == @"na" ? Country : country;
            }

            {
                var countryId = information.Country?.ElementAtOrDefault(1) ?? "";
                CountryId = CountryId != null && countryId == @"na" ? CountryId : countryId;
            }

            if (setCurrentDriversCount) {
                CurrentDriversCount = information.Clients;
            }

            Capacity = information.Capacity;
            PasswordRequired = information.Password;

            if (information.CarIds != null && Cars?.Select(x => x.Id).SequenceEqual(information.CarIds) != true) {
                Cars = information.CarIds.Select(x => Cars?.GetByIdOrDefault(x) ?? new CarEntry(x)).ToList();
                SetSelectedCarEntry(Cars.FirstOrDefault());
            }

            if (TrackId != information.TrackId) {
                TrackId = information.TrackId;
                Track = TrackId == null ? null : GetTrack(TrackId);
            }
            
            bool error;
            if (IsFullyLoaded) {
                error = SetMissingCarErrorIfNeeded(errors);
                error |= SetMissingTrackErrorIfNeeded(errors);
            } else {
                error = false;
                errors.Add("Information’s missing");
            }

            ServerStatus? result;
            if (error) {
                result = ServerStatus.Error;
            } else if (Status == ServerStatus.Error) {
                result = ServerStatus.Unloaded;
            } else {
                result = null;
            }

            var seconds = (int)Game.ConditionProperties.GetSeconds(information.Time);
            Time = $@"{seconds / 60 / 60:D2}:{seconds / 60 % 60:D2}";
            SessionEnd = DateTime.Now + TimeSpan.FromSeconds(information.TimeLeft - Math.Round(information.Timestamp / 1000d));

            if (information.SessionTypes != null) {
                var sessions = information.SessionTypes.Select((x, i) => new Session {
                    IsActive = x == information.Session,
                    Duration = information.Durations?.ElementAtOrDefault(i) ?? 0,
                    Type = (Game.SessionType)x
                }).ToList();

                if (Sessions == null || !Sessions.SequenceEqual(sessions)) {
                    Sessions = sessions;
                }
            }

            BookingMode = !information.PickUp;
            return result;
        }

        private static string IdToBb(string id, bool car = true) {
            if (car) return string.Format(ToolsStrings.Online_Server_MissingCarBbCode, id);

            id = Regex.Replace(id, @"-([^-]+)$", "/$1");
            if (!id.Contains(@"/")) id = $@"{id}/{id}";
            return string.Format(ToolsStrings.Online_Server_MissingTrackBbCode, id);
        }

        private bool SetMissingCarErrorIfNeeded([NotNull] ICollection<string> errorMessage) {
            if (!IsFullyLoaded || Cars == null) return false;

            var list = Cars.Where(x => !x.CarExists).Select(x => x.Id).ToList();
            if (!list.Any()) return false;

            errorMessage.Add(list.Count == 1
                    ? string.Format(ToolsStrings.Online_Server_CarIsMissing, IdToBb(list[0]))
                    : string.Format(ToolsStrings.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToString(@", ")));
            return true;
        }

        private bool SetMissingTrackErrorIfNeeded([NotNull] ICollection<string> errorMessage) {
            if (!IsFullyLoaded || Track != null) return false;
            errorMessage.Add(string.Format(ToolsStrings.Online_Server_TrackIsMissing, IdToBb(TrackId, false)));
            return true;
        }

        private DateTime _previousUpdateTime;

        public DateTime PreviousUpdateTime {
            get { return _previousUpdateTime; }
            private set {
                if (Equals(value, _previousUpdateTime)) return;
                _previousUpdateTime = value;
                OnPropertyChanged();
            }
        }

        private static TrackObjectBase GetTrack([NotNull] string informationId) {
            return TracksManager.Instance.GetLayoutByKunosId(informationId);
        }

        private string GetFailedReason(WebException e) {
            switch (e.Status) {
                case WebExceptionStatus.RequestCanceled:
                    return "Server did not respond in given time";
                case WebExceptionStatus.ConnectFailure:
                    return "Connect failure";
                default:
                    Logging.Warning(e.Status);
                    return "Unknown reason";
            }
        }

        public enum UpdateMode {
            Lite,
            Normal,
            Full
        }

        private bool _updating;

        public async Task Update(UpdateMode mode, bool background = false, bool fast = false) {
            if (_updating) return;
            _updating = true;

            var errors = new List<string>(3);
            var driversCount = -1;

            try {
                if (!background) {
                    CurrentDrivers = null;
                    Status = ServerStatus.Loading;
                    IsAvailable = false;
                }

                var informationUpdated = false;
                if (!IsFullyLoaded) {
                    UpdateProgress = new AsyncProgressEntry("Loading actual server information…", 0.1);

                    ServerInformationComplete loaded;
                    try {
                        loaded = await GetInformationDirectly();
                    } catch (WebException e) {
                        errors.Add($"Can’t load any information: {GetFailedReason(e)}.");
                        return;
                    }

                    if (UpdateValues(loaded, errors, false) != null) {
                        // Loaded data isn’t for this server (port by which it was loaded differs).
                        // Won’t even set drivers count in this case, whole data is obsviously wrong.
                        return;
                    }

                    driversCount = loaded.Clients;
                    informationUpdated = true;
                }

                if (mode != UpdateMode.Lite) {
                    UpdateProgress = new AsyncProgressEntry("Loading actual server information…", 0.2);

                    ServerInformationComplete loaded;
                    try {
                        loaded = await GetInformation(informationUpdated);
                    } catch (WebException e) {
                        errors.Add($"Can’t load information: {GetFailedReason(e)}.");
                        return;
                    }

                    if (loaded != null) {
                        if (loaded.Ip == Ip && loaded.PortHttp == PortHttp || informationUpdated || loaded.LoadedDirectly) {
                            // If loaded information is compatible with existing, use it immediately. Otherwise — apparently,
                            // server changed — we’ll try to load an actual data directly from it later, but only if it wasn’t
                            // loaded just before that and loaded information wasn’t loaded from it.
                            if (UpdateValues(loaded, errors, false) != null) return;
                            driversCount = loaded.Clients;
                        } else {
                            ServerInformation directlyLoaded;
                            try {
                                directlyLoaded = await GetInformationDirectly();
                            } catch (WebException e) {
                                errors.Add($"Can’t load new information: {GetFailedReason(e)}.");
                                return;
                            }

                            if (UpdateValues(directlyLoaded, errors, false) != null) return;
                            driversCount = loaded.Clients;
                        }
                    }
                }

                if (Ping == null || mode == UpdateMode.Full || !SettingsHolder.Online.PingOnlyOnce) {
                    UpdateProgress = new AsyncProgressEntry("Pinging server…", 0.3);
                    var pair = SettingsHolder.Online.ThreadsPing
                            ? await Task.Run(() => KunosApiProvider.TryToPingServer(Ip, Port, SettingsHolder.Online.PingTimeout))
                            : await KunosApiProvider.TryToPingServerAsync(Ip, Port, SettingsHolder.Online.PingTimeout);
                    if (pair != null) {
                        Ping = (long)pair.Item2.TotalMilliseconds;
                    } else {
                        Ping = null;
                        errors.Add(ToolsStrings.Online_Server_CannotPing);
                        return;
                    }
                }

                UpdateProgress = new AsyncProgressEntry("Loading players list…", 0.4);

                ServerCarsInformation information;
                try {
                    information = await KunosApiProvider.GetCarsInformationAsync(Ip, PortHttp);
                } catch (WebException e) {
                    errors.Add($"Can’t load drivers information: {GetFailedReason(e)}.");
                    return;
                }

                if (!BookingMode) {
                    CurrentDriversCount = information.Cars.Count(x => x.IsConnected);
                    driversCount = -1;
                }
                
                var currentDrivers = (BookingMode ? information.Cars : information.Cars.Where(x => x.IsConnected))
                        .Select(x => {
                            var driver = CurrentDrivers?.FirstOrDefault(y => y.Name == x.DriverName && y.Team == x.DriverTeam &&
                                    y.CarId == x.CarId && y.CarSkinId == x.CarSkinId) ?? new CurrentDriver(x);
                            return driver;
                        })
                        .ToList();
                if (CurrentDrivers == null || !CurrentDrivers.SequenceEqual(currentDrivers)) {
                    CurrentDrivers = currentDrivers;

                    var count = 0;
                    var booked = false;
                    foreach (var x in currentDrivers) {
                        if (x.IsConnected) count++;
                        if (x.IsBookedForPlayer) {
                            booked = true;
                            SetSelectedCarEntry(Cars?.GetByIdOrDefault(x.CarId, StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    ConnectedDrivers = count;
                    IsBookedForPlayer = booked;
                }

                if (Cars == null) {
                    Logging.Unexpected();
                    errors.Add("Data is still missing");
                    return;
                }

                for (int i = 0, c = Cars.Count; i < c; i++) {
                    var entry = Cars[i];

                    var wrapper = entry.CarObjectWrapper;
                    if (wrapper == null) continue;

                    /* load car if not loaded */
                    CarObject car;
                    if (wrapper.IsLoaded) {
                        car = (CarObject)wrapper.Value;
                    } else {
                        UpdateProgress = new AsyncProgressEntry($"Loading cars ({wrapper.Id})…", 0.5 + 0.4 * i / c);
                        await Task.Delay(fast ? 10 : 50);
                        car = (CarObject)await wrapper.LoadedAsync();
                    }

                    /* load skin */
                    if (!car.SkinsManager.IsLoaded) {
                        UpdateProgress = new AsyncProgressEntry($"Loading {car.DisplayName} skins…", 0.5 + 0.4 * (0.5 + i) / c);

                        await Task.Delay(fast ? 10 : 50);
                        await car.SkinsManager.EnsureLoadedAsync();
                    }

                    /* set next available skin */
                    if (CurrentSessionType == Game.SessionType.Booking) {
                        entry.AvailableSkin = car.SelectedSkin;
                        entry.Total = 0;
                        entry.Available = 0;
                        entry.IsAvailable = true;
                    } else {
                        var cars = information.Cars.Where(x => x.IsEntryList && string.Equals(x.CarId, entry.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                        string availableSkinId;

                        if (BookingMode) {
                            availableSkinId = cars.FirstOrDefault(x => x.IsRequestedGuid)?.CarSkinId;
                            entry.Total = 0;
                            entry.Available = 0;
                            entry.IsAvailable = true;
                        } else {
                            availableSkinId = cars.FirstOrDefault(y => !y.IsConnected)?.CarSkinId;
                            entry.Total = cars.Count;
                            entry.Available = cars.Count(y => !y.IsConnected);
                            entry.IsAvailable = entry.Available > 0;
                        }

                        entry.AvailableSkin = availableSkinId == null
                                ? null : availableSkinId == string.Empty ? car.GetFirstSkinOrNull() : car.GetSkinById(availableSkinId);
                    }

                    // TODO: Revert back `errors.Add(ToolsStrings.Online_Server_CarsDoNotMatch);` (?)
                }

                if (IsBookedForPlayer) {
                    FixedCar = true;
                } else {
                    FixedCar = false;
                    LoadSelectedCar();
                }
            } catch (InformativeException e) {
                errors.Add($@"{e.Message}.");
            } catch (Exception e) {
                errors.Add(string.Format(ToolsStrings.Online_Server_UnhandledError, e.Message));
                Logging.Error(e);
            } finally {
                if (driversCount != -1) {
                    CurrentDriversCount = driversCount;
                }

                UpdateProgress = AsyncProgressEntry.Ready;
                Errors = errors;
                Status = errors.Any() ? ServerStatus.Error : ServerStatus.Ready;
                AvailableUpdate();
                _updating = false;
            }
        }

        public void LoadSelectedCar() {
            if (Cars == null) return;

            var selected = LimitedStorage.Get(LimitedSpace.OnlineSelectedCar, Id);
            SetSelectedCarEntry(selected == null ? null : Cars.GetByIdOrDefault(selected));
        }

        private bool _fixedCar;

        public bool FixedCar {
            get { return _fixedCar; }
            set {
                if (Equals(value, _fixedCar)) return;
                _fixedCar = value;
                OnPropertyChanged();
            }
        }
        
        private CarEntry _selectedCarEntry;

        [CanBeNull]
        public CarEntry SelectedCarEntry {
            get { return _selectedCarEntry; }
            set {
                if (FixedCar) return;
                if (SetSelectedCarEntry(value)) {
                    LimitedStorage.Set(LimitedSpace.OnlineSelectedCar, Id, value?.Id);
                    AvailableUpdate();
                }
            }
        }

        /// <summary>
        /// Without saving. Please, check for FixedCar value before calling this method.
        /// </summary>
        /// <param name="value">New value.</param>
        public bool SetSelectedCarEntry([CanBeNull] CarEntry value) {
            if (value == null) value = Cars?.FirstOrDefault(x => x.IsAvailable) ?? Cars?.FirstOrDefault();
            if (Equals(value, _selectedCarEntry)) return false;
            _selectedCarEntry = value;
            OnPropertyChanged(nameof(SelectedCarEntry));
            return true;
        }

        /*private void SelectedCarChanged(object sender, EventArgs e) {
            SelectedCarEntry = CarsView?.CurrentItem as CarEntry;

            var selectedCar = SelectedCarEntry?.CarObject;
            LimitedStorage.Set(LimitedSpace.OnlineSelectedCar, Id, selectedCar?.Id);
            AvailableUpdate();
        }*/

        private CommandBase _addToRecentCommand;

        public ICommand AddToRecentCommand => _addToRecentCommand ?? (_addToRecentCommand = new DelegateCommand(() => {
            //RecentManagerOld.Instance.AddRecentServer(OriginalInformation);
        }, () => Status == ServerStatus.Ready /*&& RecentManagerOld.Instance.GetWrapperById(Id) == null*/));

        private ICommand _refreshCommand;

        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new DelegateCommand(() => {
            Update(UpdateMode.Full).Forget();
        }));

        /// <summary>
        /// For FileBasedOnlineSources.
        /// </summary>
        /// <returns>Description in format [IP]:[HTTP port];[Name]</returns>
        public string ToDescription() {
            return DisplayName == null ? Id : $@"{Id};{DisplayName}";
        }

        public override string ToString() {
            return Id;
        }
    }
}