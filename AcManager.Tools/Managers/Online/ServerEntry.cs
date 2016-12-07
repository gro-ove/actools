using System;
using System.Collections.Generic;
using System.Linq;
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

            Id = information.GetUniqueId();
            Ip = information.Ip;
            PortHttp = information.PortHttp;

            Ping = null;

            var errors = new List<string>(3);
            var status = UpdateValues(information, errors);
            Status = status ?? ServerStatus.Unloaded;
            Errors = errors;
        }

        public void UpdateValues([NotNull] ServerInformation information) {
            var errors = new List<string>(3);
            var status = UpdateValues(information, errors);
            if (status.HasValue) {
                Status = status.Value;
                Errors = errors;
            }
        }

        /// <summary>
        /// Sets properties based on loaded information.
        /// </summary>
        /// <param name="information">Loaded information.</param>
        /// <param name="errors">Errors will be put here.</param>
        /// <returns>Null if everything is OK, ServerStatus.Error/ServerStatus.Unloaded message otherwise.</returns>
        private ServerStatus? UpdateValues([NotNull] ServerInformation information, [NotNull] ICollection<string> errors) {
            if (Ip != information.Ip) {
                errors.Add($"IP changed (from {Ip} to {information.Ip})");
                return ServerStatus.Error;
            }

            if (PortHttp != information.PortHttp) {
                errors.Add($"HTTP port changed (from {PortHttp} to {information.PortHttp})");
                return ServerStatus.Error;
            }

            IsFullyLoaded = information.IsFullyLoaded;

            Port = information.Port;
            PortRace = information.PortRace;

            PreviousUpdateTime = DateTime.Now;
            DisplayName = information.Name == null ? Id : CleanUp(information.Name, DisplayName);

            {
                var country = information.Country?.FirstOrDefault() ?? "";
                Country = Country != null && country == @"na" ? Country : country;
            }

            {
                var countryId = information.Country?.ElementAtOrDefault(1) ?? "";
                CountryId = CountryId != null && countryId == @"na" ? CountryId : countryId;
            }

            CurrentDriversCount = information.Clients;
            Capacity = information.Capacity;

            PasswordRequired = information.Password;
            if (PasswordRequired) {
                Password = ValuesStorage.GetEncryptedString(PasswordStorageKey);
            }

            if (information.CarIds != null && Cars?.Select(x => x.Id).SequenceEqual(information.CarIds) != true) {
                Cars = information.CarIds.Select(x => Cars?.GetByIdOrDefault(x) ?? new CarEntry(x)).ToList();
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

            Sessions = information.SessionTypes?.Select((x, i) => new Session {
                IsActive = x == information.Session,
                Duration = information.Durations?.ElementAtOrDefault(i) ?? 0,
                Type = (Game.SessionType)x
            }).ToList();

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

        public enum UpdateMode {
            Lite,
            Normal,
            Full
        }

        public async Task Update(UpdateMode mode, bool background = false, bool fast = false) {
            var errors = new List<string>(3);

            try {
                if (!background) {
                    CurrentDrivers.Clear();
                    OnPropertyChanged(nameof(CurrentDrivers));

                    Status = ServerStatus.Loading;
                    IsAvailable = false;
                }

                var informationUpdated = false;
                if (!IsFullyLoaded) {
                    UpdateProgress = new AsyncProgressEntry("Loading actual server information…", 0.1);

                    var newInformation = await GetInformationDirectly();
                    if (newInformation == null) {
                        errors.Add(ToolsStrings.Online_Server_Unavailable);
                        return;
                    }

                    if (UpdateValues(newInformation, errors) != null) return;
                    informationUpdated = true;
                }

                if (mode == UpdateMode.Full) {
                    UpdateProgress = new AsyncProgressEntry("Loading actual server information…", 0.2);

                    var newInformation = await GetInformation(informationUpdated);
                    if (newInformation == null) {
                        if (!informationUpdated) {
                            errors.Add(ToolsStrings.Online_Server_CannotRefresh);
                            return;
                        }
                    } else if (newInformation.Ip == Ip && newInformation.PortHttp == PortHttp || informationUpdated || newInformation.LoadedDirectly) {
                        // If loaded information is compatible with existing, use it immediately. Otherwise — apparently,
                        // server changed — we’ll try to load an actual data directly from it later, but only if it wasn’t
                        // loaded just before that and loaded information wasn’t loaded from it.
                        if (UpdateValues(newInformation, errors) != null) return;
                    } else {
                        var directInformation = await GetInformationDirectly();
                        if (directInformation == null) {
                            errors.Add(ToolsStrings.Online_Server_Unavailable);
                            return;
                        }

                        if (UpdateValues(directInformation, errors) != null) return;
                    }
                }

                if (Ping == null || !SettingsHolder.Online.PingOnlyOnce) {
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
                var information = await KunosApiProvider.TryToGetCurrentInformationAsync(Ip, PortHttp);
                if (information == null) {
                    errors.Add(ToolsStrings.Online_Server_Unavailable);
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

                if (Cars == null) {
                    // This is not supposed to happen
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
                    if (BookingMode) {
                        entry.AvailableSkin = car.SelectedSkin;
                    } else {
                        var group = information.Cars.Where(x => x.IsEntryList && string.Equals(x.CarId, entry.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (group.Count == 0) {
                            // errors.Add(ToolsStrings.Online_Server_CarsDoNotMatch);
                            return;
                        }

                        var availableSkinId = group.FirstOrDefault(y => !y.IsConnected)?.CarSkinId;

                        entry.Total = group.Count;
                        entry.Available = group.Count(y => !y.IsConnected);
                        entry.AvailableSkin = availableSkinId == null
                                ? null : availableSkinId == string.Empty ? car.GetFirstSkinOrNull() : car.GetSkinById(availableSkinId);
                    }
                }

                /*var changed = true;
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
                }*/

                /*if (changed) {
                    LoadSelectedCar();
                }*/

                LoadSelectedCar();
            } catch (InformativeException e) {
                errors.Add($@"{e.Message}.");
            } catch (Exception e) {
                errors.Add(string.Format(ToolsStrings.Online_Server_UnhandledError, e.Message));
                Logging.Error(e);
            } finally {
                UpdateProgress = AsyncProgressEntry.Ready;
                Errors = errors;
                Status = errors.Any() ? ServerStatus.Error : ServerStatus.Ready;
                AvailableUpdate();
            }
        }

        private void LoadSelectedCar() {
            if (Cars == null) return;

            var selected = LimitedStorage.Get(LimitedSpace.OnlineSelectedCar, Id);
            var firstAvailable = (selected == null ? null : Cars.GetByIdOrDefault(selected)) ??
                    Cars.FirstOrDefault(x => x.IsAvailable) ?? Cars.FirstOrDefault();
            SetSelectedCarEntry(firstAvailable);
        }

        private CarEntry _selectedCarEntry;

        [CanBeNull]
        public CarEntry SelectedCarEntry {
            get { return _selectedCarEntry ?? Cars?.FirstOrDefault(); }
            set {
                if (Equals(value, _selectedCarEntry)) return;
                _selectedCarEntry = value;
                OnPropertyChanged();
                
                LimitedStorage.Set(LimitedSpace.OnlineSelectedCar, Id, value?.Id);
                AvailableUpdate();
            }
        }

        /// <summary>
        /// Without saving.
        /// </summary>
        /// <param name="value">New value.</param>
        private void SetSelectedCarEntry([CanBeNull] CarEntry value) {
            if (Equals(value, _selectedCarEntry)) return;
            _selectedCarEntry = value;
            OnPropertyChanged(nameof(SelectedCarEntry));
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

        public override string ToString() {
            return Id;
        }
    }
}