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

            PrepareErrorsList();
            var status = UpdateValues(information, true, true);
            Status = status == ServerStatus.MissingContent ? ServerStatus.Unloaded : status ?? ServerStatus.Unloaded;
            UpdateErrorsList();
        }

        public void UpdateValues([NotNull] ServerInformation information) {
            PrepareErrorsList();
            var status = UpdateValues(information, true, false);
            if (status.HasValue) {
                Status = status.Value;
            }
            UpdateErrorsList();
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

        private void UpdateMissingContent() {
            if (!IsFullyLoaded || Cars == null) {
                _missingCarsError = null;
            } else {
                var list = Cars.Where(x => !x.CarExists).Select(x => x.Id).ToList();
                _missingCarsError = list.Any() ? (list.Count == 1
                        ? string.Format(ToolsStrings.Online_Server_CarIsMissing, IdToBb(list[0]))
                        : string.Format(ToolsStrings.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToReadableString())) : null;
            }

            if (!IsFullyLoaded || Track != null) {
                _missingTrackError = null;
            } else {
                _missingTrackError = string.Format(ToolsStrings.Online_Server_TrackIsMissing, IdToBb(TrackId, false));
            }
        }

        /// <summary>
        /// Sets properties based on loaded information.
        /// </summary>
        /// <param name="baseInformation">Loaded information.</param>
        /// <param name="setCurrentDriversCount">Set CurrentDriversCount property.</param>
        /// <param name="forceExtendedDisabling">Set to true if PortExtended should be removed if baseInformation is not extended information.</param>
        /// <returns>Null if everything is OK, ServerStatus.Error/ServerStatus.Unloaded message otherwise.</returns>
        private ServerStatus? UpdateValues([NotNull] ServerInformation baseInformation, bool setCurrentDriversCount, bool forceExtendedDisabling) {
            if (Ip != baseInformation.Ip) {
                _updateCurrentErrors.Add($"IP changed (from {Ip} to {baseInformation.Ip})");
                return ServerStatus.Error;
            }

            if (PortHttp != baseInformation.PortHttp) {
                _updateCurrentErrors.Add($"HTTP port changed (from {PortHttp} to {baseInformation.PortHttp})");
                return ServerStatus.Error;
            }

            var information = baseInformation as ServerInformationComplete;

            if (!IsFullyLoaded || information != null) {
                int? extPort = null;
                var displayName = baseInformation.Name == null ? Id : CleanUp(baseInformation.Name, DisplayName, out extPort);

                if (baseInformation is ServerInformationExtended ext) {
                    PortExtended = ext.PortExtended;
                } else if (extPort.HasValue || !PortExtended.HasValue) {
                    PortExtended = extPort;
                } else if (forceExtendedDisabling) {
                    PortExtended = null;
                    UpdateValuesExtended(null);
                } else {
                    return null;
                }

                DisplayName = displayName;
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

            if (!IsFullyLoaded) {
                _updateCurrentErrors.Add("Information is missing");
            }

            UpdateMissingContent();

            ServerStatus? result;
            if (_missingCarsError != null || _missingTrackError != null) {
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

            var url = $"cmd://findmissing/{(car ? "car" : "track")}?param={id}";
            var basePiece = $@"[url={BbCodeBlock.EncodeAttribute(url)}]{id}[/url]";
            if (!SettingsHolder.Content.MissingContentIndexCheck ||
                    car ? !OnlineManager.IsCarAvailable(id) : !OnlineManager.IsTrackAvailable(id)) {
                return string.Format("“{0}”", basePiece);
            }

            var downloadUrl = $"cmd://downloadmissing/{(car ? "car" : "track")}?param={id}";
            return string.Format("“{0}” ({1})",
                    basePiece,
                    $@"[url={BbCodeBlock.EncodeAttribute(downloadUrl)}]download[/url]");
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