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
        private string _updatePingFailed;
        private bool _updateDriversMissing;
        private readonly List<string> _updateCurrentErrors = new List<string>();
        private readonly List<string> _updateMissingExtendedErrors = new List<string>();
        private bool _errorMissingCars, _errorMissingTrack;

        private void PrepareErrorsList() {
            _updateException = _updateWebException = null;
            _updatePingFailed = null;
            _updateDriversMissing = false;
            _updateCurrentErrors.Clear();
            _errorMissingCars = _errorMissingTrack = false;
            _updateMissingExtendedErrors.Clear();
        }

        private static string BbCodeIcon([Localizable(false)] string icon, [Localizable(false)] string command, string hint) {
            return $@"[url={BbCodeBlock.EncodeAttribute(command)}][ico={BbCodeBlock.EncodeAttribute(icon)}]{BbCodeBlock.Encode(hint)}[/ico][/url]";
        }

        private List<string> GetErrorsList() {
            var errors = new List<string>(_updateCurrentErrors);

            const string iconBulb = ".BulbIconData";
            const string iconUpgrade = ".UpdateIconData";
            const string iconDisable = ".DisableIconData";

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

            if (_updatePingFailed != null) {
                errors.Add(_updatePingFailed != string.Empty ? $"Failed to ping: {_updatePingFailed.ToSentenceMember()}" : ToolsStrings.Online_Server_CannotPing);
            }

            if (_updateDriversMissing) {
                errors.Add("Data is missing");
            }

            var cupInstall = IsAbleToInstallMissingContent.NoMissingContent;
            if (_errorMissingCars) {
                errors.Add(ErrorMessageMissingCars(ref cupInstall));
            }

            if (_errorMissingTrack) {
                errors.Add(ErrorMessageMissingTrack(ref cupInstall));
            }
            IsAbleToInstallMissingContentState_Cup = cupInstall;

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

        private string _iconUrl;

        public string IconUrl {
            get => _iconUrl;
            set => Apply(value, ref _iconUrl);
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
                        CspFeaturesList = extended.Features?.Length > 0 ? extended.Features : null;
                        _backgroundImage = extended.LoadingImageUrl;
                        IconUrl = extended.IconUrl;

                        driversCount = extended.Clients;
                        carsInformation = extended.Players;
                        informationLoadedExtended = true;
                    } catch (Exception e) {
                        // Logging.Warning($"<{Ip}:{PortHttp}> {(e.IsWebException() ? e.Message : e.ToString())}");
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
                        // Logging.Warning($"<{Ip}:{PortHttp}> {(e.IsWebException() ? e.Message : e.ToString())}");
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

                if (!informationLoadedExtended) {
                    CspFeaturesList = carsInformation.Features?.Length > 0 ? carsInformation.Features : null;
                    _backgroundImage = null;
                }

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
                // Logging.Warning($"<{Ip}:{PortHttp}> {(e.IsWebException() ? e.Message : e.ToString())}");
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
                var pair = SettingsHolder.Online.PingingSingleSocket
                        ? await KunosApiProvider.TryToPingServerAsync(Ip, Port)
                        : SettingsHolder.Online.ThreadsPing
                                ? await Task.Run(() => KunosApiProvider.TryToPingServer(Ip, Port, SettingsHolder.Online.PingTimeout, debugPinging))
                                : await KunosApiProvider.TryToPingServerAsyncOld(Ip, Port, SettingsHolder.Online.PingTimeout, debugPinging);
                if (pingId != _pingId) return;

                if (debugPinging) {
                    if (pair?.PortHttp == null) {
                        Logging.Warning($"Result: FAILED ({pair?.Error ?? "<unknown>"})");
                    } else {
                        Logging.Debug($"Result: {pair.PingTime.TotalMilliseconds:F1} ms");
                    }
                }

                if (pair?.Error != null) {
                    Ping = null;
                    _updatePingFailed = pair.Error;
                    break;
                }

                if (pair?.PortHttp != null) {
                    Ping = (long)pair.PingTime.TotalMilliseconds;
                    _updatePingFailed = null;
                    break;
                }

                if (lastAttempt) {
                    Ping = null;
                    _updatePingFailed = string.Empty;
                    // resultStatus = ServerStatus.Error;
                    // return;
                } else {
                    await Task.Delay(TimeSpan.FromSeconds(10d));
                    if (pingId != _pingId) return;
                }
            }

            UpdateErrorsList();
        }
    }
}