using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
            UpdateValuesAsync(information, true).Ignore();
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

        public async Task UpdateValuesAsync([NotNull] ServerInformation information, bool forceStatus = false) {
            PrepareErrorsList();
            var status = await UpdateValuesAsync(information, true, false);
            if (forceStatus) {
                Status = status == ServerStatus.MissingContent ? ServerStatus.Unloaded : status ?? ServerStatus.Unloaded;
            } else if (status.HasValue) {
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

        private string ErrorMessageMissingCars(ref IsAbleToInstallMissingContent stateCup) {
            if (Cars == null) return null;

            var list = Cars.Where(x => !x.CarExists).Select(x => x.Id).ToList();
            if (list.Count == 0) return null;

            var countInCup = list.Count(x => IndexDirectDownloader.IsCarAvailable(x) == IndexDirectDownloader.ContentState.Available);
            if (countInCup == list.Count) stateCup = IsAbleToInstallMissingContent.AllOfIt;
            else if (countInCup > 0) stateCup = IsAbleToInstallMissingContent.Partially;
            
            return list.Count == 1
                    ? string.Format(ToolsStrings.Online_Server_CarIsMissing, IdToBb(list[0]))
                    : string.Format(ToolsStrings.Online_Server_CarsAreMissing, list.Select(x => IdToBb(x)).JoinToReadableString());
        }

        private string ErrorMessageMissingTrack(ref IsAbleToInstallMissingContent stateCup) {
            if (TrackId == null || Track != null) return null;
            // Triggered after ErrorMessageMissingCars()
            if (stateCup != IsAbleToInstallMissingContent.Partially
                    && IndexDirectDownloader.IsTrackAvailable(TrackId) == IndexDirectDownloader.ContentState.Available) {
                stateCup = IsAbleToInstallMissingContent.AllOfIt;
            }
            return string.Format(ToolsStrings.Online_Server_TrackIsMissing, IdToBb(TrackId, false));
        }

        private void UpdateMissingContent() {
            _errorMissingCars = Cars != null && Cars.Any(x => !x.CarExists);
            _errorMissingTrack = TrackId != null && Track == null;
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
        private async Task<ServerStatus?> UpdateValuesAsync([NotNull] ServerInformation baseInformation, bool setCurrentDriversCount,
                bool forceExtendedDisabling) {
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

            var country = information.Country?.FirstOrDefault() ?? "";
            Country = Country != null && country == @"na" ? Country : country;

            var countryId = information.Country?.ArrayElementAtOrDefault(1) ?? "";
            CountryId = CountryId != null && countryId == @"na" ? CountryId : countryId;

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
                } else if (trackIdPieces.Length == 3) {
                    RequiredCspVersion = Math.Max(PatchHelper.MinimumTestOnlineVersion, trackIdPieces[0].As(0));
                    var value = ServerPresetObject.EncodeSymbols.IndexOf(trackIdPieces[1].FirstOrDefault());
                    CspExtendedCarsPhysics = (value & 1) == 1;
                    CspExtendedTrackPhysics = (value & 2) == 2;
                    CheckCspState();
                } else {
                    RequiredCspVersion = 0;
                    CheckCspState();
                }

                if (trackIdPieces.Last() != TrackId) {
                    TrackId = trackIdPieces.Last();
                    Track = TrackId == null ? null : await GetTrackAsync(TrackId);
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
            if (_errorMissingCars || _errorMissingTrack) {
                result = SettingsHolder.Online.LoadServersWithMissingContent ? ServerStatus.MissingContent : ServerStatus.Error;
            } else if (Status == ServerStatus.Error) {
                result = ServerStatus.Unloaded;
            } else {
                result = null;
            }

            var seconds = (int)Game.ConditionProperties.GetSeconds(information.Time);
            Time = seconds.ToDisplayTime();
            TimeSeconds = seconds;
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
            const string searchIcon = ".MagnifyIconData";
            const string linkIcon = ".DownloadIconData";
            const string webIcon = ".WebIconData";

            if (!car) {
                id = Regex.Replace(id, @"-([^-]+)$", "/$1");
            }

            var name = $@"“{BbCodeBlock.Encode(id)}”";
            var typeLocalized = car ? ToolsStrings.AdditionalContent_Car : ToolsStrings.AdditionalContent_Track;

            var searchUrl = $"cmd://findMissing/{(car ? "car" : "track")}?param={id}";
            var searchLink = string.Format(@"[url={0}][ico={1}]Look for missing {2} online[/ico][/url]", BbCodeBlock.EncodeAttribute(searchUrl),
                    BbCodeBlock.EncodeAttribute(searchIcon), typeLocalized);

            var version = car ? GetRequiredCarVersion(id) : GetRequiredTrackVersion();
            var foundState = OptionIgnoreIndexIfServerProvidesSpecificVersion && version != null
                    ? IndexDirectDownloader.ContentState.Unknown
                    : car ? IndexDirectDownloader.IsCarAvailable(id) : IndexDirectDownloader.IsTrackAvailable(id);
            if (foundState == IndexDirectDownloader.ContentState.Unknown) {
                return $@"{name} \[{searchLink}]";
            }

            var downloadUrl = $"cmd://downloadMissing/{(car ? "car" : "track")}?param={(string.IsNullOrWhiteSpace(version) ? id : $@"{id}|{version}")}";
            var downloadLink = foundState == IndexDirectDownloader.ContentState.Limited
                    ? $@"[url={BbCodeBlock.EncodeAttribute(downloadUrl)}][ico={BbCodeBlock.EncodeAttribute(webIcon)}]Missing {typeLocalized} is found; click to open its webpage[/ico][/url]"
                    : $@"[url={BbCodeBlock.EncodeAttribute(downloadUrl)}][ico={BbCodeBlock.EncodeAttribute(linkIcon)}]Missing {typeLocalized} is found; click to download[/ico][/url]";
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

        private static Task<TrackObjectBase> GetTrackAsync([NotNull] string informationId) {
            return TracksManager.Instance.GetLayoutByKunosIdAsync(informationId);
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