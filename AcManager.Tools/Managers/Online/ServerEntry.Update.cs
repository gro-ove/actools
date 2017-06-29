using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
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
        private string _missingCarsError, _missingTrackError;

        private void PrepareErrorsList() {
            _updateException = _updateWebException = null;
            _updatePingFailed = _updateDriversMissing = false;
            _updateCurrentErrors.Clear();
            _missingCarsError = _missingTrackError = null;
            _updateMissingExtendedErrors.Clear();
        }

        private void UpdateErrorsList() {
            var errors = new List<string>(_updateCurrentErrors);

            if (_updateException != null) {
                errors.Add((_updateException as InformativeException)?.Message ??
                        string.Format(ToolsStrings.Online_Server_UnhandledError, _updateException.Message.ToSentenceMember()));
            }

            if (_updateWebException != null) {
                errors.Add($"Can’t load any information: {GetFailedReason(_updateWebException)}.");
            }

            if (_updatePingFailed) {
                errors.Add(ToolsStrings.Online_Server_CannotPing);
            }

            if (_updateDriversMissing) {
                errors.Add("Data is still missing");
            }

            if (_missingCarsError != null) {
                errors.Add(_missingCarsError);
            }

            if (_missingTrackError != null) {
                errors.Add(_missingTrackError);
            }

            if (PortExtended != null) {
                errors.AddRange(_updateMissingExtendedErrors);
            }

            Errors = errors;
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
                    UpdateProgress = new AsyncProgressEntry("Loading actual server information…", 0.1);

                    ServerInformationComplete loaded;
                    try {
                        loaded = await GetInformationDirectly();
                    } catch (WebException e) {
                        _updateWebException = e;
                        resultStatus = ServerStatus.Error;
                        return;
                    }

                    var update = UpdateValues(loaded, false, true);
                    if (update != null) {
                        resultStatus = update.Value;
                        if (update != ServerStatus.MissingContent) {
                            // Loaded data isn’t for this server (port by which it was loaded differs).
                            // Won’t even set drivers count in this case, whole data is obsviously wrong.
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

                if (PortExtended != null) {
                    try {
                        var extended = await GetExtendedInformationDirectly();
                        var update = UpdateValues(extended, false, true);

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
                        Logging.Warning(e);
                        PortExtended = null;
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
                    UpdateProgress = new AsyncProgressEntry("Loading actual server information…", 0.2);

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
                            var update = UpdateValues(loaded, false, true);
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

                            var update = UpdateValues(directlyLoaded, false, true);
                            if (update != null) {
                                resultStatus = update.Value;
                                if (update != ServerStatus.MissingContent) return;
                            }
                            driversCount = loaded.Clients;
                        }
                    }
                }

                // Ping server
                if (Ping == null || mode == UpdateMode.Full || !SettingsHolder.Online.PingOnlyOnce) {
                    UpdateProgress = new AsyncProgressEntry("Pinging server…", 0.3);
                    var pair = SettingsHolder.Online.ThreadsPing
                            ? await Task.Run(() => KunosApiProvider.TryToPingServer(Ip, Port, SettingsHolder.Online.PingTimeout))
                            : await KunosApiProvider.TryToPingServerAsync(Ip, Port, SettingsHolder.Online.PingTimeout);
                    if (pair != null) {
                        Ping = (long)pair.Item2.TotalMilliseconds;
                        _updatePingFailed = false;
                    } else {
                        Ping = null;
                        _updatePingFailed = true;
                        resultStatus = ServerStatus.Error;
                        return;
                    }
                }

                // Load players list
                if (carsInformation == null) {
                    UpdateProgress = new AsyncProgressEntry("Loading players list…", 0.4);

                    try {
                        carsInformation = await KunosApiProvider.GetCarsInformationAsync(Ip, PortHttp);
                    } catch (WebException e) {
                        _updateWebException = e;
                        resultStatus = ServerStatus.Error;
                        return;
                    }
                }

                if (!BookingMode) {
                    CurrentDriversCount = carsInformation.Cars.Count(x => x.IsConnected);
                    driversCount = -1;
                }

                var currentDrivers = (BookingMode ? carsInformation.Cars : carsInformation.Cars.Where(x => x.IsConnected))
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
                    _updateDriversMissing = true;
                    resultStatus = ServerStatus.Error;
                    return;
                }

                for (int i = 0, c = Cars.Count; i < c; i++) {
                    var entry = Cars[i];

                    var wrapper = entry.CarObjectWrapper;
                    CarObject car;

                    // Load car if not loaded
                    if (wrapper != null) {
                        if (wrapper.IsLoaded) {
                            car = (CarObject)wrapper.Value;
                        } else {
                            UpdateProgress = new AsyncProgressEntry($"Loading cars ({wrapper.Id})…", 0.5 + 0.4 * i / c);
                            await Task.Delay(fast ? 10 : 50);
                            car = (CarObject)await wrapper.LoadedAsync();
                        }
                    } else {
                        car = null;
                    }

                    // Load skin
                    if (car?.SkinsManager.IsLoaded == false) {
                        UpdateProgress = new AsyncProgressEntry($"Loading {car.DisplayName} skins…", 0.5 + 0.4 * (0.5 + i) / c);

                        await Task.Delay(fast ? 10 : 50);
                        await car.SkinsManager.EnsureLoadedAsync();
                    }

                    // Set next available skin
                    if (CurrentSessionType == Game.SessionType.Booking) {
                        entry.AvailableSkinId = car?.SelectedSkin?.Id;
                        entry.Total = 0;
                        entry.Available = 0;
                        entry.IsAvailable = true;
                    } else {
                        var cars = carsInformation.Cars.Where(x => x.IsEntryList && string.Equals(x.CarId, entry.Id, StringComparison.OrdinalIgnoreCase)).ToList();
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

                        entry.AvailableSkinId = availableSkinId;
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
            } catch (Exception e) {
                _updateException = e;
                resultStatus = ServerStatus.Error;
                Logging.Error(e);
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
    }
}
