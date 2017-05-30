using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
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
            Status = status == ServerStatus.MissingContent ? ServerStatus.Unloaded : status ?? ServerStatus.Unloaded;
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

        [CanBeNull]
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
                int? extPort = null;
                DisplayName = baseInformation.Name == null ? Id : CleanUp(baseInformation.Name, DisplayName, out extPort);
                ActualName = baseInformation.Name ?? Id;
                PortExtended = baseInformation is ServerInformationExtended ext ? ext.PortExtended : extPort;
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

            bool missingContent;
            if (IsFullyLoaded) {
                var missingCar = SetMissingCarErrorIfNeeded(errors);
                var missingTrack = SetMissingTrackErrorIfNeeded(errors);
                missingContent = missingCar || missingTrack;

                AllCarsAvailable = !missingCar;
                AllContentAvailable = !missingContent;
            } else {
                missingContent = false;
                AllCarsAvailable = true;
                AllContentAvailable = true;
                errors.Add("Information’s missing");
            }

            ServerStatus? result;
            if (missingContent) {
                result = SettingsHolder.Online.LoadServersWithMissingContent ? ServerStatus.MissingContent : ServerStatus.Error;
            } else if (Status == ServerStatus.Error) {
                result = ServerStatus.Unloaded;
            } else {
                result = null;
            }

            var seconds = (int)Game.ConditionProperties.GetSeconds(information.Time);
            Time = $@"{seconds / 60 / 60:D2}:{seconds / 60 % 60:D2}";
            SessionEnd = DateTime.Now + TimeSpan.FromSeconds(information.TimeLeft - Math.Round(information.Timestamp / 1000d));
            RaceMode = information.Timed ? information.Extra ? RaceMode.TimedExtra : RaceMode.Timed : RaceMode.Laps;

            if (information.SessionTypes != null) {
                var sessions = information.SessionTypes.Select((x, i) => new Session {
                    IsActive = x == information.Session,
                    Duration = information.Durations?.ElementAtOrDefault(i) ?? 0,
                    Type = (Game.SessionType)x,
                    RaceMode = RaceMode
                }).ToList();

                if (Sessions == null || !Sessions.SequenceEqual(sessions)) {
                    Sessions = sessions;
                }
            }

            BookingMode = !information.PickUp;
            return result;
        }

        private static string IdToBb(string id, bool car = true) {
            if (!car) {
                id = Regex.Replace(id, @"-([^-]+)$", "/$1");
            }

            // var url = SettingsHolder.Content.MissingContentSearch.GetUri(id, car ? SettingsHolder.MissingContentType.Car : SettingsHolder.MissingContentType.Track);
            var url = $"cmd://findmissing/{(car ? "car" : "track")}?param={id}";
            return string.Format("“{0}”", $@"[url={BbCodeBlock.EncodeAttribute(url)}]{id}[/url]");
        }

        private bool SetMissingCarErrorIfNeeded([NotNull] ICollection<string> errorMessage) {
            if (!IsFullyLoaded || Cars == null) return false;

            var list = Cars.Where(x => !x.CarExists).Select(x => x.Id).ToList();
            if (!list.Any()) return false;

            errorMessage.Add(list.Count == 1
                    ? string.Format(ToolsStrings.Online_Server_CarIsMissing, IdToBb(list[0]))
                    : string.Format(ToolsStrings.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToReadableString()));
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
            RaiseSelectedCarChanged();
            return true;
        }

        public void RaiseSelectedCarChanged() {
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