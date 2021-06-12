using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        public static string OptionDebugPing = null;

        private static string GetFailedReason(WebException e) {
            switch (e.Status) {
                case WebExceptionStatus.RequestCanceled:
                case WebExceptionStatus.Timeout:
                    return "Server did not respond in given time";
                case WebExceptionStatus.ConnectFailure:
                    return "Connect failure";
                default:
                    Logging.Warning(e.Status);
                    return "Unknown reason";
            }
        }

        private Exception _updateException;
        private WebException _updateWebException;
        private bool _updatePingFailed, _updateDriversMissing;
        private readonly List<string> _updateCurrentErrors = new List<string>();
        private readonly List<string> _updateMissingExtendedErrors = new List<string>();
        private bool _errorMissingCars, _errorMissingTrack;

        private void PrepareErrorsList() {
            _updateException = _updateWebException = null;
            _updatePingFailed = _updateDriversMissing = false;
            _updateCurrentErrors.Clear();
            _errorMissingCars = _errorMissingTrack = false;
            _updateMissingExtendedErrors.Clear();
        }

        private static string BbCodeIcon([Localizable(false)] string icon, [Localizable(false)] string command, string hint) {
            return $@"[url={BbCodeBlock.EncodeAttribute(command)}][ico={BbCodeBlock.EncodeAttribute(icon)}]{BbCodeBlock.Encode(hint)}[/ico][/url]";
        }

        private List<string> GetErrorsList() {
            var errors = new List<string>(_updateCurrentErrors);

            const string iconBulb =
                    "F1 M 34.8333,60.1667L 34.8333,57.3958L 41.1667,58.5833L 41.1667,60.1667L 34.8333,60.1667 Z M 31.6666,55.0209L 31.6666,52.25L 44.3333,53.8334L 44.3333,56.6042L 31.6666,55.0209 Z M 44.3333,51.8542L 31.6666,50.2709L 31.6666,47.5L 44.3333,49.0834L 44.3333,51.8542 Z M 38,17.4167C 44.9956,17.4167 50.6667,23.4422 50.6667,30.875C 50.6667,35.8565 44.3333,40.7324 44.3333,42.5329L 44.3333,47.5L 31.6667,45.9167L 31.6667,42.5329C 31.6667,41.1667 25.3333,35.8565 25.3333,30.875C 25.3333,23.4422 31.0044,17.4167 38,17.4167 Z";
            const string iconUpgrade =
                    "F1 M 37.8516,35.625L 34.6849,38.7917L 23.6016,50.2708L 23.6016,39.9792L 37.8516,24.9375L 52.1016,39.9792L 52.1016,50.2708L 41.0182,38.7917L 37.8516,35.625 Z";
            const string iconDisable =
                    "F1 M 36.4167,36.4167L 36.4167,17.4167L 41.1667,17.4167L 41.1667,36.4167L 36.4167,36.4167 Z M 57,39.5833C 57,50.0767 48.4934,58.5833 38,58.5833C 27.5066,58.5833 19,50.0767 19,39.5833C 19,30.7301 25.0552,23.2911 33.25,21.1819L 33.25,27.8374C 28.6079,29.7165 25.3333,34.2675 25.3333,39.5833C 25.3333,46.5789 31.0044,52.25 38,52.25C 44.9956,52.25 50.6667,46.5789 50.6667,39.5833C 50.6667,34.8949 48.1194,30.8014 44.3333,28.6113L 44.3333,21.6645C 51.7129,24.2728 57,31.3106 57,39.5833 Z";

            if (CspRequiredMissing) {
                if (RequiredCspVersion == PatchHelper.NonExistentVersion) {
                    errors.Add($"Custom Shaders Patch is not allowed here [{BbCodeIcon(iconDisable, "cmd://csp/disable", "Disable Custom Shaders Patch")}]");
                } else {
                    if (PatchHelper.GetInstalledBuild().As(-1) >= RequiredCspVersion && !PatchHelper.IsActive()) {
                        errors.Add($"Custom Shaders Patch is required [{BbCodeIcon(iconBulb, "cmd://csp/enable", "Enable Custom Shaders Patch")}]");
                    } else {
                        var version = PatchUpdater.Instance.Versions
                                .Where(x => x.Build >= RequiredCspVersion && x.AvailableToDownload).MinEntryOrDefault(x => x.Build);
                        if (version != null) {
                            var upgradeButton = BbCodeIcon(iconUpgrade, $"cmd://csp/update?param={version.Build}", "Update CSP to " + version.Version);
                            errors.Add($"Custom Shaders Patch v{version.Version} or newer is required [{upgradeButton}]");
                        } else {
                            errors.Add($"Custom Shaders Patch with version ID {RequiredCspVersion} or newer is required");
                        }
                    }
                }
            }

            if (_updateException != null) {
                errors.Add((_updateException as InformativeException)?.Message ??
                        string.Format(ToolsStrings.Online_Server_UnhandledError, _updateException.Message.ToSentenceMember()));
            }

            if (_updateWebException != null) {
                errors.Add($"Can’t load any information: {GetFailedReason(_updateWebException).ToSentenceMember()}.");
            }

            if (_updatePingFailed) {
                errors.Add(ToolsStrings.Online_Server_CannotPing);
            }

            if (_updateDriversMissing) {
                errors.Add("Data is missing");
            }

            if (_errorMissingCars) {
                errors.Add(ErrorMessageMissingCars());
            }

            if (_errorMissingTrack) {
                errors.Add(ErrorMessageMissingTrack());
            }

            if (HasDetails) {
                errors.AddRange(_updateMissingExtendedErrors);
            }
            return errors;
        }

        private void UpdateErrorsList() {
            _errorsReady = false;
            _errorsString = null;
            OnPropertyChanged(nameof(Errors));
            OnPropertyChanged(nameof(ErrorsString));
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
            _pingId++;

            var driversCount = -1;
            var resultStatus = ServerStatus.Ready;

            try {
                // If it’s a background update, don’t change anything in UI to avoid flashing
                if (!background) {
                    CurrentDrivers = null;
                    Status = ServerStatus.Loading;
                    IsAvailable = false;
                }

                // Reset some update-state values
                PrepareErrorsList();

                // Nothing loaded at all!
                var informationUpdated = false;
                if (!IsFullyLoaded) {
                    UpdateProgress = new AsyncProgressEntry(ToolsStrings.Online_LoadingActualInformation, 0.1);

                    ServerInformationComplete loaded;
                    try {
                        loaded = await GetInformationDirectly();
                    } catch (HttpRequestException e) {
                        if (e.InnerException is WebException webException) {
                            _updateWebException = webException;
                        } else {
                            _updateException = e;
                        }
                        resultStatus = ServerStatus.Error;
                        return;
                    } catch (WebException e) {
                        _updateWebException = e;
                        resultStatus = ServerStatus.Error;
                        return;
                    }

                    var update = await UpdateValuesAsync(loaded, false, true);
                    if (update != null) {
                        resultStatus = update.Value;
                        if (update != ServerStatus.MissingContent) {
                            // Loaded data isn’t for this server (port by which it was loaded differs).
                            // Won’t even set drivers count in this case, whole data is obviously wrong.
                            return;
                        }
                    }

                    driversCount = loaded.Clients;

                    // Set this flag to True so we won’t use GetInformationDirectly() again later
                    informationUpdated = true;
                }

                // Extended mode for server wrapping thing
                var informationLoadedExtended = false;
                ServerCarsInformation carsInformation = null;

                if (DetailsPort != null) {
                    try {
                        var extended = await GetExtendedInformationDirectly();
                        var update = await UpdateValuesAsync(extended, false, true);

                        if (update != null) {
                            resultStatus = update.Value;
                            if (update != ServerStatus.MissingContent) {
                                UpdateValuesExtended(null);
                                return;
                            }
                        }

                        UpdateValuesExtended(extended);

                        driversCount = extended.Clients;
                        carsInformation = extended.Players;
                        informationLoadedExtended = true;
                    } catch (Exception e) {
                        Logging.Warning($"<{Ip}:{PortHttp}> {(e.IsWebException() ? e.Message : e.ToString())}");
                        DetailsPort = null;
                        UpdateValuesExtended(null);
                        return;
                    }
                } else if (DetailsId != null) {
                    try {
                        var extended = await CmApiProvider.GetOnlineDataAsync(DetailsId);
                        if (extended != null) {
                            Country = extended.Country?.FirstOrDefault() ?? Country;
                            CountryId = extended.Country?.ArrayElementAtOrDefault(1) ?? CountryId;
                            Sessions?.ForEach((x, i) => x.Duration = extended.Durations?.ElementAtOrDefault(i) ?? x.Duration);
                        }
                        UpdateValuesExtended(extended);
                    } catch (Exception e) {
                        Logging.Warning($"<{Ip}:{PortHttp}> {(e.IsWebException() ? e.Message : e.ToString())}");
                        UpdateValuesExtended(null);
                        return;
                    }
                } else {
                    UpdateValuesExtended(null);
                }

                // Update information
                if (!informationLoadedExtended && (mode != UpdateMode.Lite ||
                        !(Sessions?.Count > 0) // if there are no sessions (!), maybe information is damaged, let’s re-download
                        )) {
                    UpdateProgress = new AsyncProgressEntry(ToolsStrings.Online_LoadingActualInformation, 0.2);

                    ServerInformationComplete loaded;
                    try {
                        // If informationUpdated is True and settings set to update-information-directly mode, this method
                        // will return 0.
                        loaded = await GetInformation(informationUpdated);
                    } catch (WebException e) {
                        _updateWebException = e;
                        resultStatus = ServerStatus.Error;
                        return;
                    }

                    if (loaded != null) {
                        if (loaded.Ip == Ip && loaded.PortHttp == PortHttp || informationUpdated || loaded.LoadedDirectly) {
                            // If loaded information is compatible with existing, use it immediately. Otherwise — apparently,
                            // server changed — we’ll try to load an actual data directly from it later, but only if it wasn’t
                            // loaded just before that and loaded information wasn’t loaded from it.
                            var update = await UpdateValuesAsync(loaded, false, true);
                            if (update != null) {
                                resultStatus = update.Value;
                                if (update != ServerStatus.MissingContent) return;
                            }
                            driversCount = loaded.Clients;
                        } else {
                            ServerInformation directlyLoaded;
                            try {
                                directlyLoaded = await GetInformationDirectly();
                            } catch (WebException e) {
                                _updateWebException = e;
                                resultStatus = ServerStatus.Error;
                                return;
                            }

                            var update = await UpdateValuesAsync(directlyLoaded, false, true);
                            if (update != null) {
                                resultStatus = update.Value;
                                if (update != ServerStatus.MissingContent) return;
                            }
                            driversCount = loaded.Clients;
                        }
                    }
                }

                // Load players list
                if (carsInformation == null) {
                    UpdateProgress = new AsyncProgressEntry(ToolsStrings.Online_LoadingPlayersList, 0.4);

                    try {
                        carsInformation = await KunosApiProvider.GetCarsInformationAsync(Ip, PortHttp);
                    } catch (WebException e) {
                        _updateWebException = e;
                        resultStatus = ServerStatus.Error;
                        return;
                    }
                }

                CspFeaturesList = carsInformation.Features?.Length > 0 ? carsInformation.Features : null;
                if (!BookingMode) {
                    CurrentDriversCount = carsInformation.Cars.Count(x => x.IsConnected);
                    driversCount = -1;
                }

                var currentDrivers = (BookingMode ? carsInformation.Cars : carsInformation.Cars.Where(x => x.IsConnected))
                        .Select(x => {
                            var driver = CurrentDrivers?.FirstOrDefault(y => y.SameAs(x)) ?? new CurrentDriver(x, RequiredCspVersion != 0);
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
                    _updateDriversMissing = true;
                    resultStatus = ServerStatus.Error;
                    return;
                }

                for (int i = 0, c = Cars.Count; i < c; i++) {
                    var entry = Cars[i];

                    var wrapper = entry.CarWrapper;
                    CarObject car;

                    // Load car if not loaded
                    if (wrapper != null) {
                        if (wrapper.IsLoaded) {
                            car = (CarObject)wrapper.Value;
                        } else {
                            UpdateProgress = new AsyncProgressEntry(string.Format(ToolsStrings.Online_LoadingCars, wrapper.Id), 0.5 + 0.4 * i / c);
                            await Task.Delay(fast ? 10 : 50);
                            car = (CarObject)await wrapper.LoadedAsync();
                        }

                        car.SubscribeWeak(OnContentNameChanged);
                    } else {
                        car = null;
                    }

                    // Load skin
                    if (car?.SkinsManager.IsLoaded == false) {
                        UpdateProgress = new AsyncProgressEntry(string.Format(ToolsStrings.Online_LoadingSkins, car.DisplayName), 0.5 + 0.4 * (0.5 + i) / c);

                        await Task.Delay(fast ? 10 : 50);
                        await car.SkinsManager.EnsureLoadedAsync();
                    }

                    // Set next available skin
                    if (CurrentSessionType == Game.SessionType.Booking) {
                        entry.SetAvailableSkinId(car?.SelectedSkin?.Id, null);
                        entry.Total = 0;
                        entry.Available = 0;
                        entry.IsAvailable = true;
                    } else {
                        var cars = carsInformation.Cars.Where(x => x.IsEntryList
                                && string.Equals(x.CarId, entry.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                        ServerActualCarInformation availableSkin;

                        if (BookingMode) {
                            availableSkin = cars.FirstOrDefault(x => x.IsRequestedGuid);
                            entry.Total = 0;
                            entry.Available = 0;
                            entry.IsAvailable = true;
                        } else {
                            availableSkin = cars.FirstOrDefault(y => !y.IsConnected);
                            entry.Total = cars.Count;
                            entry.Available = cars.Count(y => !y.IsConnected);
                            entry.IsAvailable = entry.Available > 0;
                        }

                        entry.SetAvailableSkinId(availableSkin?.CarSkinId, RequiredCspVersion == 0 ? null : availableSkin?.CspParams);
                    }
                }

                var missingContentUpdate = UpdateMissingContentExtended(resultStatus == ServerStatus.MissingContent);
                if (missingContentUpdate.HasValue) {
                    resultStatus = missingContentUpdate.Value;
                }

                if (IsBookedForPlayer) {
                    FixedCar = true;
                } else {
                    FixedCar = false;
                    LoadSelectedCar();
                }

                // Ping server
                if (Ping == null || mode == UpdateMode.Full || !SettingsHolder.Online.PingOnlyOnce) {
                    if (mode == UpdateMode.Lite) {
                        await TryToPing();
                    } else {
                        TryToPing().Ignore();
                    }
                }
            } catch (Exception e) {
                Logging.Warning($"<{Ip}:{PortHttp}> {(e.IsWebException() ? e.Message : e.ToString())}");
                _updateException = e;
                resultStatus = ServerStatus.Error;
            } finally {
                if (driversCount != -1) {
                    CurrentDriversCount = driversCount;
                }

                UpdateProgress = AsyncProgressEntry.Ready;
                Status = !SettingsHolder.Online.LoadServersWithMissingContent && resultStatus == ServerStatus.MissingContent ?
                        ServerStatus.Error : resultStatus;
                UpdateMissingContent();
                UpdateErrorsList();
                AvailableUpdate();
                _updating = false;
            }
        }

        private int _pingId;

        private async Task TryToPing() {
            var pingId = ++_pingId;
            for (var attemptsLeft = Math.Max(SettingsHolder.Online.PingAttempts, 1); attemptsLeft > 0; attemptsLeft--) {
                var lastAttempt = attemptsLeft == 1;

                var debugPinging = Ip == OptionDebugPing;
                if (debugPinging) {
                    Logging.Debug("Pinging THAT server, attempts left: " + (attemptsLeft - 1));
                    Logging.Debug("Timeout: " + SettingsHolder.Online.PingTimeout);
                    Logging.Debug("Threads pinging: " + SettingsHolder.Online.ThreadsPing);
                }

                UpdateProgress = new AsyncProgressEntry("Pinging server…", 0.3);
                var pair = SettingsHolder.Online.ThreadsPing
                        ? await Task.Run(() => KunosApiProvider.TryToPingServer(Ip, Port, SettingsHolder.Online.PingTimeout, debugPinging))
                        : await KunosApiProvider.TryToPingServerAsync(Ip, Port, SettingsHolder.Online.PingTimeout, debugPinging);
                if (pingId != _pingId) return;

                if (debugPinging) {
                    if (pair == null) {
                        Logging.Warning("Result: FAILED");
                    } else {
                        Logging.Debug($"Result: {pair.Item2.TotalMilliseconds:F1} ms");
                    }
                }

                if (pair != null) {
                    Ping = (long)pair.Item2.TotalMilliseconds;
                    _updatePingFailed = false;
                    break;
                }

                if (lastAttempt) {
                    Ping = null;
                    _updatePingFailed = true;
                    // resultStatus = ServerStatus.Error;
                    // return;
                } else {
                    await Task.Delay(150);
                    if (pingId != _pingId) return;
                }
            }

            UpdateErrorsList();
        }
    }
}