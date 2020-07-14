using System;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry : Displayable, IWithId {
        public static bool OptionIgnoreIndexIfServerProvidesSpecificVersion = true;

        public ServerEntry([NotNull] ServerInformation information) {
            if (information == null) throw new ArgumentNullException(nameof(information));
            _sortingName = Lazier.Create(() => GetSortingName(DisplayName));

            Id = information.Id;
            Ip = information.Ip;
            PortHttp = information.PortHttp;
            Ping = null;

            PrepareErrorsList();
            var status = UpdateValues(information, true, true);
            Status = status == ServerStatus.MissingContent ? ServerStatus.Unloaded : status ?? ServerStatus.Unloaded;
            UpdateErrorsList();
        }

        public override string DisplayName {
            get => base.DisplayName;
            set {
                if (Equals(value, base.DisplayName)) return;
                base.DisplayName = value;
                _sortingName.Reset();
            }
        }

        private readonly Lazier<string> _sortingName;
        public string SortingName => _sortingName.Value;

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
            get => _actualName;
            set => Apply(value, ref _actualName);
        }

        private void UpdateMissingContent() {
            if (Cars == null) {
                _missingCarsError = null;
            } else {
                var list = Cars.Where(x => !x.CarExists).Select(x => x.Id).ToList();
                _missingCarsError = list.Any() ? (list.Count == 1
                        ? string.Format(ToolsStrings.Online_Server_CarIsMissing, IdToBb(list[0]))
                        : string.Format(ToolsStrings.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToReadableString())) : null;
            }

            if (TrackId == null || Track != null) {
                _missingTrackError = null;
            } else {
                _missingTrackError = string.Format(ToolsStrings.Online_Server_TrackIsMissing, IdToBb(TrackId, false));
            }
        }

        public void CheckCspState() {
            var missingBefore = CspRequiredMissing;
            if (RequiredCspVersion == PatchHelper.NonExistentVersion) {
                CspRequiredAvailable = PatchHelper.GetActiveBuild() == null;
                CspRequiredMissing = PatchHelper.GetActiveBuild() != null;
            } else if (RequiredCspVersion > 0) {
                CspRequiredAvailable = PatchHelper.GetActiveBuild().As(-1) >= RequiredCspVersion;
                CspRequiredMissing = !CspRequiredAvailable;
            } else {
                CspRequiredAvailable = false;
                CspRequiredMissing = false;
            }

            if (CspRequiredMissing != missingBefore) {
                UpdateErrorsList();
                AvailableUpdate();
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
                string detailsId = null;
                var displayName = baseInformation.Name == null ? Id : CleanUp(baseInformation.Name, DisplayName, out extPort, out detailsId);

                if (baseInformation is ServerInformationExtended ext) {
                    DetailsPort = ext.PortExtended;
                } else if (extPort.HasValue || !DetailsPort.HasValue) {
                    DetailsPort = extPort;
                } else if (forceExtendedDisabling) {
                    DetailsPort = null;
                    UpdateValuesExtended(null);
                } else {
                    return null;
                }

                if (DetailsPort == null && detailsId != null) {
                    // Logging.Write($"Details for {displayName}: {detailsId}");
                    DetailsId = detailsId;
                } else {
                    DetailsId = null;
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
                var countryId = information.Country?.ArrayElementAtOrDefault(1) ?? "";
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

            if (information.TrackId != null) {
                var trackIdPieces = information.TrackId.Substring(information.TrackId.StartsWith(@"csp/") ? 4 : 0)
                        .Split(new[] { @"/../" }, StringSplitOptions.None);
                if (trackIdPieces.Length == 2) {
                    RequiredCspVersion = Math.Max(PatchHelper.MinimumTestOnlineVersion, trackIdPieces[0].As(0));
                    CheckCspState();
                } else {
                    RequiredCspVersion = 0;
                    CheckCspState();
                }

                if (trackIdPieces.Last() != TrackId) {
                    TrackId = trackIdPieces.Last();
                    Track = TrackId == null ? null : GetTrack(TrackId);
                }
            } else if (TrackId != null) {
                TrackId = null;
                Track = null;
                RequiredCspVersion = 0;
                CheckCspState();
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
            Time = seconds.ToDisplayTime();
            SessionEnd = DateTime.Now + TimeSpan.FromSeconds(information.TimeLeft - Math.Round(information.Timestamp / 1000d));
            RaceMode = information.Timed ? information.Extra ? RaceMode.TimedExtra : RaceMode.Timed : RaceMode.Laps;

            if (information.SessionTypes != null) {
                var sessions = information.SessionTypes.Select((x, i) => new Session {
                    IsActive = x == information.Session,
                    Duration = information.Durations?.ArrayElementAtOrDefault(i) ?? 0,
                    Type = (Game.SessionType)x,
                    RaceMode = RaceMode,
                    Inverted = information.Inverted
                }).ToList();

                if (Sessions == null || !Sessions.SequenceEqual(sessions)) {
                    Sessions = sessions;
                }
            }

            BookingMode = !information.PickUp;
            return result;
        }

        private string IdToBb(string id, bool car = true) {
            const string searchIcon =
                    "F1 M 42.5,22C 49.4036,22 55,27.5964 55,34.5C 55,41.4036 49.4036,47 42.5,47C 40.1356,47 37.9245,46.3435 36,45.2426L 26.9749,54.2678C 25.8033,55.4393 23.9038,55.4393 22.7322,54.2678C 21.5607,53.0962 21.5607,51.1967 22.7322,50.0251L 31.7971,40.961C 30.6565,39.0755 30,36.8644 30,34.5C 30,27.5964 35.5964,22 42.5,22 Z M 42.5,26C 37.8056,26 34,29.8056 34,34.5C 34,39.1944 37.8056,43 42.5,43C 47.1944,43 51,39.1944 51,34.5C 51,29.8056 47.1944,26 42.5,26 Z";
            const string linkIcon =
                    "F1 M 23.4963,46.1288L 25.0796,48.8712L 29.4053,50.0303L 33.519,47.6553L 34.8902,46.8636L 37.6326,45.2803L 38.4242,46.6515L 37.2652,50.9772L 30.4091,54.9356L 21.7577,52.6174L 18.591,47.1326L 20.9091,38.4811L 27.7652,34.5227L 32.0909,35.6818L 32.8826,37.053L 30.1402,38.6364L 28.769,39.428L 24.6553,41.803L 23.4963,46.1288 Z M 38.7348,28.1895L 45.5908,24.2311L 54.2423,26.5493L 57.409,32.0341L 55.0908,40.6856L 48.2348,44.6439L 43.9091,43.4848L 43.1174,42.1136L 45.8598,40.5303L 47.231,39.7386L 51.3446,37.3636L 52.5037,33.0379L 50.9204,30.2955L 46.5946,29.1364L 42.481,31.5114L 41.1098,32.3031L 38.3674,33.8864L 37.5757,32.5152L 38.7348,28.1895 Z M 33.9006,45.1496L 31.7377,44.5701L 30.5502,42.5133L 31.1298,40.3504L 42.0994,34.0171L 44.2623,34.5966L 45.4498,36.6534L 44.8702,38.8163L 33.9006,45.1496 Z";

            if (!car) {
                id = Regex.Replace(id, @"-([^-]+)$", "/$1");
            }

            var name = $@"“{BbCodeBlock.Encode(id)}”";
            var typeLocalized = car ? ToolsStrings.AdditionalContent_Car : ToolsStrings.AdditionalContent_Track;

            var searchUrl = $"cmd://findMissing/{(car ? "car" : "track")}?param={id}";
            var searchLink = string.Format(@"[url={0}][ico={1}]Look for missing {2} online[/ico][/url]", BbCodeBlock.EncodeAttribute(searchUrl),
                    BbCodeBlock.EncodeAttribute(searchIcon), typeLocalized);

            var version = car ? GetRequiredCarVersion(id) : GetRequiredTrackVersion();
            if (OptionIgnoreIndexIfServerProvidesSpecificVersion && version != null ||
                    (car ? !IndexDirectDownloader.IsCarAvailable(id) : !IndexDirectDownloader.IsTrackAvailable(id))) {
                return $@"{name} \[{searchLink}]";
            }

            var downloadUrl = $"cmd://downloadMissing/{(car ? "car" : "track")}?param={(string.IsNullOrWhiteSpace(version) ? id : $@"{id}|{version}")}";
            var downloadLink = string.Format(@"[url={0}][ico={1}]{2} is found; click to open its page or hold Ctrl and click to start downloading[/ico][/url]",
                    BbCodeBlock.EncodeAttribute(downloadUrl), BbCodeBlock.EncodeAttribute(linkIcon), typeLocalized.ToTitle());
            return $@"“{BbCodeBlock.Encode(id)}” \[{searchLink}, {downloadLink}]";
        }

        private DateTime _previousUpdateTime;

        public DateTime PreviousUpdateTime {
            get => _previousUpdateTime;
            private set => Apply(value, ref _previousUpdateTime);
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
            get => _fixedCar;
            set => Apply(value, ref _fixedCar);
        }

        private CarEntry _selectedCarEntry;

        [CanBeNull]
        public CarEntry SelectedCarEntry {
            get => _selectedCarEntry;
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

        private DelegateCommand _addToRecentCommand;

        public DelegateCommand AddToRecentCommand => _addToRecentCommand ?? (_addToRecentCommand = new DelegateCommand(() => {
            //RecentManagerOld.Instance.AddRecentServer(OriginalInformation);
        }, () => Status == ServerStatus.Ready /*&& RecentManagerOld.Instance.GetWrapperById(Id) == null*/));

        private AsyncCommand _refreshCommand;

        public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(() => Update(UpdateMode.Full)));

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