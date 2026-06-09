using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins.CspCommands;
using AcManager.Tools.AcPlugins.Helpers;
using AcManager.Tools.AcPlugins.Info;
using AcManager.Tools.AcPlugins.Kunos;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcPlugins {
    public sealed class AcServerPluginManager : IDisposable {
        public const int RequiredProtocolVersion = 4;

        public int ProtocolVersion { get; private set; }

        /// <summary>
        /// Gives the last timestamp where the acServer was active, used for KeepAlive
        /// </summary>
        public DateTime LastServerActivity { get; private set; }

        public SessionInfo CurrentSession => _currentSession;

        public SessionInfo PreviousSession => _previousSession;

        public DriverInfo[] GetDriverInfos() {
            lock (_lockObject) {
                return _currentSession.Drivers.ToArray();
            }
        }

        public bool TryGetDriverInfo(byte carId, out DriverInfo driver) {
            lock (_lockObject) {
                return _carUsedByDictionary.TryGetValue(carId, out driver);
            }
        }

        public DriverInfo GetDriverInfo(byte carId) {
            if (TryGetDriverInfo(carId, out var driver)) {
                return driver;
            } else {
                return null;
            }
        }

        public DriverInfo GetDriverByConnectionId(int connectionId) {
            return _currentSession.Drivers[connectionId];
        }

        #region Private stuff
        [NotNull]
        private readonly AcServerPluginManagerSettings _settings;

        private readonly DuplexUdpClient _udp;
        private readonly List<IAcServerPlugin> _plugins;
        private readonly List<ExternalPluginInfo> _externalPlugins;
        private readonly Dictionary<ExternalPluginInfo, DuplexUdpClient> _openExternalPlugins;
        private readonly List<ISessionReportHandler> _sessionReportHandlers = new List<ISessionReportHandler>();
        private readonly object _lockObject = new object();

        private readonly Dictionary<byte, DriverInfo> _carUsedByDictionary = new Dictionary<byte, DriverInfo>();
        private int _lastCarUpdateCarId = -1;
        private SessionInfo _currentSession = new SessionInfo();
        private SessionInfo _previousSession = new SessionInfo();

        private MsgSessionInfo _nextSessionStarting;
        #endregion

        public AcServerPluginManager([NotNull] AcServerPluginManagerSettings settings) {
            _settings = settings;
            _currentSession.MaxClients = settings.Capacity;

            _plugins = new List<IAcServerPlugin>();
            _udp = new DuplexUdpClient();
            _externalPlugins = new List<ExternalPluginInfo>();
            _openExternalPlugins = new Dictionary<ExternalPluginInfo, DuplexUdpClient>();

            ProtocolVersion = -1;

            if (settings.ConnectAutomatically) {
                ConnectDelay().Ignore();
            }
        }

        public int MaxClients() {
            return _currentSession.MaxClients;
        }

        private async Task ConnectDelay() {
            await Task.Yield();
            if (!IsConnected) {
                Connect();
            }
        }

        public void AddInternalPlugin(string assemblyName, string typeName) {
            var assembly = Assembly.Load(assemblyName);
            var type = assembly.GetType(typeName);
            var plugin = (IAcServerPlugin)Activator.CreateInstance(type);
            AddPlugin(plugin);
        }

        public void AddSessionReportHandler(ISessionReportHandler handler) {
            _sessionReportHandlers.Add(handler);
        }

        public void AddPlugin(IAcServerPlugin plugin) {
            lock (_lockObject) {
                if (plugin == null) {
                    throw new ArgumentNullException(nameof(plugin));
                }

                if (IsConnected) {
                    throw new Exception("Cannot add plugin while connected.");
                }

                if (_plugins.Contains(plugin)) {
                    throw new Exception("Plugin was added before.");
                }

                _plugins.Add(plugin);

                plugin.OnInit(this);
            }
        }

        [NotNull]
        public T AddPlugin<T>() where T : IAcServerPlugin, new() {
            var ret = new T();
            AddPlugin(ret);
            return ret;
        }

        public void RemovePlugin(IAcServerPlugin plugin) {
            lock (_lockObject) {
                if (plugin == null) {
                    throw new ArgumentNullException(nameof(plugin));
                }

                if (IsConnected) {
                    throw new Exception("Cannot remove plugin while connected.");
                }

                if (!_plugins.Contains(plugin)) {
                    throw new Exception("Plugin was not added before.");
                }

                _plugins.Remove(plugin);
            }
        }

        public void AddExternalPlugin(ExternalPluginInfo externalPlugin) {
            lock (_lockObject) {
                if (externalPlugin == null) {
                    throw new ArgumentNullException(nameof(externalPlugin));
                }

                if (IsConnected) {
                    throw new Exception("Cannot add external plugin while connected.");
                }

                if (_externalPlugins.Contains(externalPlugin)) {
                    throw new Exception("External plugin was added before.");
                }

                _externalPlugins.Add(externalPlugin);
            }
        }

        public void RemoveExternalPlugin(ExternalPluginInfo externalPlugin) {
            lock (_lockObject) {
                if (externalPlugin == null) {
                    throw new ArgumentNullException(nameof(externalPlugin));
                }

                if (IsConnected) {
                    throw new Exception("Cannot remove external plugin while connected.");
                }

                if (!_externalPlugins.Contains(externalPlugin)) {
                    throw new Exception("External plugin was not added before.");
                }

                _externalPlugins.Remove(externalPlugin);
            }
        }

        private DriverInfo GetDriverReportForCarId(byte carId) {
            if (!_carUsedByDictionary.TryGetValue(carId, out var driverReport)) {
                // It seems we missed the OnNewConnection for this driver
                driverReport = new DriverInfo {
                    ConnectionId = _currentSession.Drivers.Count,
                    ConnectedTimestamp = DateTime.UtcNow.Ticks, // Obviously not correct but better than nothing
                    CarId = carId
                };

                _currentSession.Drivers.Add(driverReport);
                _carUsedByDictionary.Add(driverReport.CarId, driverReport);
                RequestCarInfo(carId);
            } else if (string.IsNullOrEmpty(driverReport.DriverGuid)) {
                // It seems we did not yet receive carInfo yet, request again
                RequestCarInfo(carId);
            }

            return driverReport;
        }

        private void SetSessionInfo(MsgSessionInfo msg, bool isNewSession) {
            _currentSession.ServerName = msg.ServerName;
            _currentSession.TrackName = msg.Track;
            _currentSession.TrackConfig = msg.TrackConfig;
            _currentSession.SessionName = msg.Name;
            _currentSession.SessionType = msg.SessionType;
            _currentSession.SessionDuration = msg.SessionDuration;
            _currentSession.LapCount = msg.Laps;
            _currentSession.WaitTime = msg.WaitTime;
            if (isNewSession) {
                _currentSession.Timestamp = msg.CreationDate.ToUniversalTime().Ticks;
            }
            _currentSession.AmbientTemp = msg.AmbientTemp;
            _currentSession.RoadTemp = msg.RoadTemp;
            _currentSession.Weather = msg.Weather;
            _currentSession.RealtimeUpdateInterval = (ushort)_settings.RealtimeUpdateInterval.TotalMilliseconds;
            // TODO: Set MaxClients when added to msg

            // Here you might want to start a new session:
            /*if (isNewSession && StartNewLogOnNewSession > 0 && this.Logger is IFileLog) {
                ((IFileLog)this.Logger).StartLoggingToFile(
                        new DateTime(_currentSession.Timestamp, DateTimeKind.Utc).ToString("yyyyMMdd_HHmmss") + "_"
                                + _currentSession.TrackName + "_" + _currentSession.SessionName + ".log");
            }*/
        }

        private void FinalizeAndStartNewReport() {
            try {
                // Update PlayerConnections with results
                foreach (var connection in _currentSession.Drivers) {
                    var laps = _currentSession.Laps.Where(l => l.ConnectionId == connection.ConnectionId).ToList();
                    var validLaps = laps.Where(l => l.Cuts == 0).ToList();
                    if (validLaps.Count > 0) {
                        connection.BestLap = validLaps.Min(l => l.LapTime);
                    } else if (_currentSession.SessionType != Game.SessionType.Race) {
                        // Temporarily set BestLap to MaxValue for easier sorting for qualifying/practice results
                        connection.BestLap = int.MaxValue;
                    }

                    if (laps.Count > 0) {
                        connection.TotalTime = (uint)laps.Sum(l => l.LapTime);
                        connection.LapCount = laps.Max(l => l.LapNo);
                        connection.Incidents += laps.Sum(l => l.Cuts);
                    }
                }

                if (_currentSession.SessionType == Game.SessionType.Race) {
                    ushort position = 1;

                    // Compute start position
                    foreach (var connection in _currentSession.Drivers
                            .Where(d => d.ConnectedTimestamp >= 0 && d.ConnectedTimestamp <= _currentSession.Timestamp)
                            .OrderByDescending(d => d.StartSplinePosition)) {
                        connection.StartPosition = position++;
                    }

                    foreach (var connection in _currentSession.Drivers
                            .Where(d => d.ConnectedTimestamp >= 0 && d.ConnectedTimestamp > _currentSession.Timestamp)
                            .OrderBy(d => d.ConnectedTimestamp)) {
                        connection.StartPosition = position++;
                    }

                    foreach (var connection in _currentSession.Drivers.Where(d => d.ConnectedTimestamp < 0)) {
                        connection.StartPosition = position++;
                    }

                    // Compute end position
                    position = 1;
                    var winnerLapCount = 0;
                    var winnerTime = 0U;

                    var sortedDrivers = new List<DriverInfo>(_currentSession.Drivers.Count);

                    sortedDrivers.AddRange(_currentSession.Drivers.Where(d => d.LapCount == _currentSession.LapCount).OrderBy(GetLastLapTimestamp));
                    sortedDrivers.AddRange(_currentSession.Drivers.Where(d => d.LapCount != _currentSession.LapCount)
                            .OrderByDescending(d => d.LapCount).ThenByDescending(d => d.EndSplinePosition));

                    foreach (var connection in sortedDrivers) {
                        if (position == 1) {
                            winnerLapCount = connection.LapCount;
                            winnerTime = connection.TotalTime;
                        }
                        connection.Position = position++;

                        if (connection.LapCount == winnerLapCount) {
                            // Is incorrect for players connected after race started
                            connection.Gap = FormatTimespan((int)connection.TotalTime - (int)winnerTime);
                        } else {
                            if (winnerLapCount - connection.LapCount == 1) {
                                connection.Gap = "1 lap";
                            } else {
                                connection.Gap = (winnerLapCount - connection.LapCount) + " laps";
                            }
                        }
                    }
                } else {
                    ushort position = 1;
                    var winnerTime = 0U;
                    foreach (var connection in _currentSession.Drivers.OrderBy(d => d.BestLap)) {
                        if (position == 1) {
                            winnerTime = connection.BestLap;
                        }

                        connection.Position = position++;

                        if (connection.BestLap == int.MaxValue) {
                            connection.BestLap = 0; // reset best lap
                        } else {
                            connection.Gap = FormatTimespan((int)connection.BestLap - (int)winnerTime);
                        }
                    }
                }

                if (_currentSession.Drivers.Count > 0) {
                    foreach (var handler in _sessionReportHandlers) {
                        try {
                            handler.HandleReport(_currentSession);
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                }
            } finally {
                _previousSession = _currentSession;
                _currentSession = new SessionInfo {
                    MaxClients = _previousSession.MaxClients // TODO: can be removed when MaxClients added to MsgSessionInfo
                };
                _lastCarUpdateCarId = -1;

                foreach (var connection in _previousSession.Drivers) {
                    if (_carUsedByDictionary.TryGetValue(connection.CarId, out var found) && found == connection) {
                        var recreatedConnection = new DriverInfo {
                            ConnectionId = _currentSession.Drivers.Count,
                            ConnectedTimestamp = found.ConnectedTimestamp,
                            DisconnectedTimestamp = found.DisconnectedTimestamp, // should be not set yet
                            DriverGuid = found.DriverGuid,
                            DriverName = found.DriverName,
                            DriverTeam = found.DriverTeam,
                            CarId = found.CarId,
                            CarModel = found.CarModel,
                            CarSkin = found.CarSkin,
                            BallastKg = found.BallastKg,
                            IsAdmin = found.IsAdmin
                        };

                        _currentSession.Drivers.Add(recreatedConnection);
                    }
                }

                // Clear the dictionary of cars currently used
                _carUsedByDictionary.Clear();
                foreach (var recreatedConnection in _currentSession.Drivers) {
                    _carUsedByDictionary.Add(recreatedConnection.CarId, recreatedConnection);
                }
            }
        }

        public bool IsConnected => _udp.Opened;

        public void Connect() {
            lock (_lockObject) {
                if (IsConnected) {
                    throw new Exception("PluginManager already connected");
                }

                ProtocolVersion = -1;
                _udp.Open(_settings.ListeningPort, _settings.RemoteHostName, _settings.RemotePort,
                        MessageReceived, msg => Logging.Warning(msg));

                try {
                    OnConnected();
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }

                foreach (var externalPlugin in _externalPlugins) {
                    try {
                        var externalPluginUdp = new DuplexUdpClient();
                        externalPluginUdp.Open(externalPlugin.ListeningPort, externalPlugin.RemoteHostname, externalPlugin.RemotePort,
                                MessageReceivedFromExternalPlugin, msg => Logging.Warning(msg));
                        _openExternalPlugins.Add(externalPlugin, externalPluginUdp);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            }
        }

        private void MessageReceivedFromExternalPlugin(TimestampedBytes tsb) {
            _udp.Send(tsb);

            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + AcMessageParser.Parse(tsb));
            }
        }

        public void Disconnect() {
            if (!IsConnected) {
                throw new Exception("PluginManager is not connected");
            }

            _udp.Close();

            lock (_lockObject) {
                try {
                    foreach (var externalPluginUdp in _openExternalPlugins.Values) {
                        try {
                            externalPluginUdp.Close();
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                    _openExternalPlugins.Clear();
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }

                try {
                    OnDisconnected();
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }
            }
        }

        private void MessageReceived(TimestampedBytes data) {
            LastServerActivity = DateTime.Now;

            lock (_lockObject) {
                var msg = AcMessageParser.Parse(data);

                if (ProtocolVersion == -1 && (msg is MsgVersionInfo || msg is MsgSessionInfo)) {
                    if (msg is MsgVersionInfo info) {
                        ProtocolVersion = info.Version;
                    } else {
                        ProtocolVersion = ((MsgSessionInfo)msg).Version;
                    }

                    if (ProtocolVersion != RequiredProtocolVersion) {
                        Disconnect();
                        throw new Exception(
                                $"AcServer protocol version {ProtocolVersion} is different from the required protocol version {RequiredProtocolVersion}. Disconnecting…");
                    }
                }

                try {
                    switch (msg.Type) {
                        case ACSProtocol.MessageType.ACSP_SESSION_INFO:
                            OnSessionInfo((MsgSessionInfo)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_NEW_SESSION:
                            OnNewSession((MsgSessionInfo)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_NEW_CONNECTION:
                            OnNewConnection((MsgNewConnection)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_CONNECTION_CLOSED:
                            OnConnectionClosed((MsgConnectionClosed)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_CAR_UPDATE:
                            OnCarUpdate((MsgCarUpdate)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_CAR_INFO:
                            OnCarInfo((MsgCarInfo)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_LAP_COMPLETED:
                            OnLapCompleted((MsgLapCompleted)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_END_SESSION:
                            OnSessionEnded((MsgSessionEnded)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_CLIENT_EVENT:
                            OnCollision((MsgClientEvent)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_VERSION:
                            OnProtocolVersion((MsgVersionInfo)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_CLIENT_LOADED:
                            OnClientLoaded((MsgClientLoaded)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_CHAT:
                            OnChatMessage((MsgChat)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_ERROR:
                            OnServerError((MsgError)msg);
                            break;
                        case ACSProtocol.MessageType.ACSP_REALTIMEPOS_INTERVAL:
                        case ACSProtocol.MessageType.ACSP_GET_CAR_INFO:
                        case ACSProtocol.MessageType.ACSP_SEND_CHAT:
                        case ACSProtocol.MessageType.ACSP_BROADCAST_CHAT:
                        case ACSProtocol.MessageType.ACSP_GET_SESSION_INFO:
                            throw new Exception("Received unexpected MessageType (for a plugin): " + msg.Type);
                        case ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_CAR:
                        case ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_ENV:
                        case ACSProtocol.MessageType.ERROR_BYTE:
                            throw new Exception("Received wrong or unknown MessageType: " + msg.Type);
                        default:
                            throw new Exception("Received wrong or unknown MessageType: " + msg.Type);
                    }
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }

                foreach (var externalPluginUdp in _openExternalPlugins.Values) {
                    externalPluginUdp.TrySend(data);
                }
            }
        }

        private void OnConnected() {
            try {
                // If we do not receive the session Info in the next 3 seconds request info (async).
                ThreadPool.QueueUserWorkItem(o => {
                    try {
                        Thread.Sleep(3000);
                        if (ProtocolVersion == -1) {
                            RequestSessionInfo(-1);
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                });

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnConnected();
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }

                // If the KeepAlive monitor is configured, we'll start a endless loop
                // that will try to determine a killed acServer.
                if (_settings.AcServerKeepAliveInterval > TimeSpan.Zero) {
                    ThreadPool.QueueUserWorkItem(o => {
                        while (true) {
                            // Sleeping for some seconds
                            Thread.Sleep(_settings.AcServerKeepAliveInterval);

                            // Everything is ok if we had either didn’t see anything so far (server still off)
                            // or we had server activity in the past <interval> seconds.
                            if (LastServerActivity != DateTime.MinValue
                                    && LastServerActivity + _settings.AcServerKeepAliveInterval < DateTime.Now) {
                                // Now if the last Server Activity is older than our KeepAlive interval
                                // we’ll initiate a version check — probably the cheapest way to detect
                                // if the server is alive.
                                // if there is a realtime interval set, this should only fire when the
                                // server is empty and idling around in a P/Q session. So some messages
                                // won’t hurt anybody.

                                // Update: There is no Version Request? Damn. Then a session one?
                                RequestSessionInfo(-1);

                                // Then we’ll give the server some time to answer this request, which
                                // should set the date.
                                Thread.Sleep(1500);

                                // Still late?
                                if (LastServerActivity + _settings.AcServerKeepAliveInterval < DateTime.Now) {
                                    // We’ll go to the “server is dead” state so we won’t repeat this until the next
                                    // “real” timeout.
                                    LastServerActivity = DateTime.MinValue;

                                    foreach (var plugin in _plugins) {
                                        try {
                                            plugin.OnAcServerTimeout();
                                        } catch (Exception ex) {
                                            Logging.Warning(ex);
                                        }
                                    }
                                } else {
                                    // No? That means the server was silent for more than the KeepAlive range, but is still responsive.
                                    // May be weird, but some plugins need to know this
                                    foreach (var plugin in _plugins) {
                                        try {
                                            plugin.OnAcServerAlive();
                                        } catch (Exception ex) {
                                            Logging.Warning(ex);
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnDisconnected() {
            try {
                FinalizeAndStartNewReport();
                _currentSession.Drivers.Clear();
                _carUsedByDictionary.Clear();

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnDisconnected();
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnSessionInfo(MsgSessionInfo msg) {
            try {
                var firstSessionInfo = _currentSession.SessionType == 0;
                SetSessionInfo(msg, firstSessionInfo);
                if (firstSessionInfo) {
                    // First time we received session info, also enable real time update
                    if (_settings.RealtimeUpdateInterval > TimeSpan.Zero) {
                        EnableRealtimeReport(_settings.RealtimeUpdateInterval);
                    }
                    // request car info for all cars
                    for (var i = 0; i < _currentSession.MaxClients; i++) {
                        RequestCarInfo((byte)i);
                    }
                }

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnSessionInfo(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnNewSession(MsgSessionInfo msg) {
            try {
                _nextSessionStarting = msg;

                var firstSessionInfo = _currentSession.SessionType == 0;
                if (!firstSessionInfo && _settings.NewSessionStartDelay > TimeSpan.Zero) {
                    ThreadPool.QueueUserWorkItem(o => {
                        Thread.Sleep(_settings.NewSessionStartDelay);
                        StartNewSession(msg);
                    });
                } else {
                    StartNewSession(msg);
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void StartNewSession(MsgSessionInfo msg) {
            lock (_lockObject) {
                if (msg != _nextSessionStarting) {
                    return;
                }

                _nextSessionStarting = null;

                try {
                    FinalizeAndStartNewReport();
                    _currentSession.MissedSessionStart = false;
                    SetSessionInfo(msg, true);

                    if (_settings.RealtimeUpdateInterval > TimeSpan.Zero) {
                        EnableRealtimeReport(_settings.RealtimeUpdateInterval);
                    }

                    foreach (var plugin in _plugins) {
                        try {
                            plugin.OnNewSession(msg);
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }
            }
        }

        private void OnSessionEnded(MsgSessionEnded msg) {
            try {
                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnSessionEnded(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnNewConnection(MsgNewConnection msg) {
            try {
                var newConnection = new DriverInfo {
                    ConnectionId = _currentSession.Drivers.Count,
                    ConnectedTimestamp = DateTime.UtcNow.Ticks,
                    DriverGuid = msg.DriverGuid,
                    DriverName = msg.DriverName,
                    DriverTeam = string.Empty, // missing in msg
                    CarId = msg.CarId,
                    CarModel = msg.CarModel,
                    CarSkin = msg.CarSkin,
                    BallastKg = 0 // missing in msg
                };

                _currentSession.Drivers.Add(newConnection);

                if (_carUsedByDictionary.TryGetValue(newConnection.CarId, out var otherDriver)) {
                    // should not happen
                    Logging.Warning($"Car is already used by another driver (manager: {GetHashCode()}, cars dict.: {_carUsedByDictionary.GetHashCode()})");
                    otherDriver.DisconnectedTimestamp = DateTime.UtcNow.Ticks;
                    _carUsedByDictionary[msg.CarId] = newConnection;
                } else {
                    _carUsedByDictionary.Add(newConnection.CarId, newConnection);
                }

                // request car info to get additional info and check when driver really is connected
                RequestCarInfo(msg.CarId);

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnNewConnection(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnConnectionClosed(MsgConnectionClosed msg) {
            try {
                if (_carUsedByDictionary.TryGetValue(msg.CarId, out var driverReport)) {
                    if (msg.DriverGuid == driverReport.DriverGuid) {
                        driverReport.DisconnectedTimestamp = DateTime.UtcNow.Ticks;
                        _carUsedByDictionary.Remove(msg.CarId);
                    } else {
                        Logging.Warning("MsgOnConnectionClosed DriverGuid does not match Guid of connected driver");
                    }
                } else {
                    Logging.Warning("Car was not known to be in use");
                }

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnConnectionClosed(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnCarInfo(MsgCarInfo msg) {
            try {
                if (_carUsedByDictionary.TryGetValue(msg.CarId, out var driverReport)) {
                    driverReport.CarModel = msg.CarModel;
                    driverReport.CarSkin = msg.CarSkin;
                    driverReport.DriverName = msg.DriverName;
                    driverReport.DriverTeam = msg.DriverTeam;
                    driverReport.DriverGuid = msg.DriverGuid;
                }

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnCarInfo(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnCarUpdate(MsgCarUpdate msg) {
            try {
                // We check if this is the first CarUpdate message for this round (they seem to be sent in a bulk and ordered by carId)
                // If that's the case we trigger OnBulkCarUpdateFinished
                // The trick with the connectedDriversCount is used as a fail-safe when single messages are received out of order
                var connectedDriversCount = CurrentSession.Drivers.Count(d => d.IsConnected);
                var isBulkUpdate = _lastCarUpdateCarId - msg.CarId >= connectedDriversCount / 2;
                _lastCarUpdateCarId = msg.CarId;

                // Ignore updates in the first 10 seconds of the session
                if (_nextSessionStarting == null && DateTime.UtcNow.Ticks - _currentSession.Timestamp > 10 * 10000000) {
                    if (isBulkUpdate) {
                        // Ok, this was the last one, so the last updates are like a snapshot within a millisecond or less.
                        // Great spot to examine positions, overtakes and stuff where multiple cars are compared to each other

                        OnBulkCarUpdateFinished();

                        // In every case we let the plugins do their calculations - before even raising the OnCarUpdate(msg). This function could
                        // Take advantage of updated DriverInfos
                        foreach (var plugin in _plugins) {
                            try {
                                plugin.OnBulkCarUpdateFinished();
                            } catch (Exception ex) {
                                Logging.Warning(ex);
                            }
                        }
                    }

                    var driver = GetDriverReportForCarId(msg.CarId);
                    driver.UpdatePosition(msg, _settings.RealtimeUpdateInterval);

                    //if (sw == null)
                    //{
                    //    sw = new StreamWriter(@"c:\workspace\positions.csv");
                    //    sw.AutoFlush = true;
                    //}
                    //sw.WriteLine(ToSingle3(msg.WorldPosition).ToString() + ", " + ToSingle3(msg.Velocity).Length());

                    foreach (var plugin in _plugins) {
                        try {
                            plugin.OnCarUpdate(msg);
                            plugin.OnCarUpdate(driver);
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnBulkCarUpdateFinished() {
            // So we'll try to compare the cars towards each other, because currently all DriverInfos
            // are up-to-date and comparable

            // First: CurrentDistanceToClosestCar
            // We'll just do a simple list of the moving cars that is ordered by the SplinePos. This doesn't respect
            // Finding the position across the finish line, but this is a minor thing for now
            CurrentSession.Drivers.ForEach(x => x.CurrentDistanceToClosestCar = 0);
            var sortedDrivers = CurrentSession.Drivers.Where(x => x.CurrentSpeed > 30).OrderBy(x => x.EndSplinePosition).ToArray();
            if (sortedDrivers.Length > 1) {
                var prev = sortedDrivers[sortedDrivers.Length - 1];
                for (var i = 0; i < sortedDrivers.Length; i++) {
                    var next = sortedDrivers[i];
                    var distance = (prev.LastPosition - next.LastPosition).Length();

                    if (prev.CurrentDistanceToClosestCar > distance || prev.CurrentDistanceToClosestCar == 0) {
                        prev.CurrentDistanceToClosestCar = distance;
                    }

                    if (next.CurrentDistanceToClosestCar > distance || next.CurrentDistanceToClosestCar == 0) {
                        next.CurrentDistanceToClosestCar = distance;
                    }

                    prev = next;
                }
            }
        }

        private void OnCollision(MsgClientEvent msg) {
            try {
                // Ignore collisions in the first 10 seconds of the session
                if (_nextSessionStarting == null && DateTime.UtcNow.Ticks - _currentSession.Timestamp > 10 * 10000000) {
                    var driver = GetDriverReportForCarId(msg.CarId);
                    var withOtherCar = msg.Subtype == (byte)ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_CAR;

                    driver.Incidents += withOtherCar ? 2 : 1; // TODO: only if relVel > thresh

                    DriverInfo driver2 = null;
                    if (withOtherCar) {
                        driver2 = GetDriverReportForCarId(msg.OtherCarId);
                        driver2.Incidents += 2; // TODO: only if relVel > thresh
                    }

                    var incident = new IncidentInfo {
                        Type = msg.Subtype,
                        Timestamp = DateTime.UtcNow.Ticks,
                        ConnectionId1 = driver.ConnectionId,
                        ConnectionId2 = withOtherCar ? driver2.ConnectionId : -1,
                        ImpactSpeed = msg.RelativeVelocity,
                        WorldPosition = msg.WorldPosition,
                        RelPosition = msg.RelativePosition,
                    };

                    _currentSession.Incidents.Add(incident);

                    foreach (var plugin in _plugins) {
                        try {
                            plugin.OnCollision(msg);
                            plugin.OnCollision(incident);
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnLapCompleted(MsgLapCompleted msg) {
            try {
                var driver = GetDriverReportForCarId(msg.CarId);

                var lapLength = driver.OnLapCompleted();

                ushort position = 0;
                ushort lapNo = 0;
                for (var i = 0; i < msg.LeaderboardSize; i++) {
                    if (msg.Leaderboard[i].CarId == msg.CarId) {
                        position = (byte)(i + 1);
                        lapNo = msg.Leaderboard[i].Laps;
                        break;
                    }
                }

                if (!_currentSession.MissedSessionStart && _currentSession.SessionType == Game.SessionType.Race) {
                    // For race compute Position based on own info (better with disconnected drivers)
                    position = (ushort)(_currentSession.Laps.Count(l => l.LapNo == lapNo) + 1);
                }

                var lap = new LapInfo {
                    ConnectionId = driver.ConnectionId,
                    Timestamp = DateTime.UtcNow.Ticks,
                    LapTime = msg.LapTime,
                    LapLength = lapLength,
                    LapNo = lapNo,
                    Position = position,
                    Cuts = msg.Cuts,
                    GripLevel = msg.GripLevel
                };

                _currentSession.Laps.Add(lap);

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnLapCompleted(msg);
                        plugin.OnLapCompleted(lap);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnClientLoaded(MsgClientLoaded msg) {
            try {
                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnClientLoaded(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnChatMessage(MsgChat msg) {
            try {
                if (TryGetDriverInfo(msg.CarId, out var driver)) {
                    if (!driver.IsAdmin && !string.IsNullOrWhiteSpace(_settings.AdminPassword)
                            && msg.Message.StartsWith("/admin ", StringComparison.InvariantCultureIgnoreCase)) {
                        driver.IsAdmin = msg.Message.Substring("/admin ".Length).Equals(_settings.AdminPassword);
                    }

                    if (driver.IsAdmin) {
                        if (msg.Message.StartsWith("/send_chat ", StringComparison.InvariantCultureIgnoreCase)) {
                            var carIdStartIdx = "/send_chat ".Length;
                            var carIdEndIdx = msg.Message.IndexOf(' ', carIdStartIdx);
                            if (carIdEndIdx > carIdStartIdx && byte.TryParse(msg.Message.Substring(carIdStartIdx, carIdEndIdx - carIdStartIdx), out var carId)) {
                                var chatMsg = msg.Message.Substring(carIdEndIdx);
                                SendChatMessage(carId, chatMsg);
                            } else {
                                SendChatMessage(msg.CarId, "Invalid car id provided");
                            }
                        } else if (msg.Message.StartsWith("/broadcast ", StringComparison.InvariantCultureIgnoreCase)) {
                            var broadcastMsg = msg.Message.Substring("/broadcast ".Length);
                            BroadcastChatMessage(broadcastMsg);
                        }
                    }
                }

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnChatMessage(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnProtocolVersion(MsgVersionInfo msg) {
            try {
                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnProtocolVersion(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void OnServerError(MsgError msg) {
            try {
                if (_settings.LogServerErrors) {
                    Logging.Warning("Server error: " + msg.ErrorMessage);
                }

                foreach (var plugin in _plugins) {
                    try {
                        plugin.OnServerError(msg);
                    } catch (Exception ex) {
                        Logging.Warning(ex);
                    }
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        public void ProcessEnteredCommand(string cmd) {
            lock (_lockObject) {
                try {
                    foreach (var plugin in _plugins) {
                        try {
                            if (plugin.OnCommandEntered(cmd)) {
                                break;
                            }
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }
            }
        }

        #region Requests to the AcServer
        public void RequestCarInfo(byte carId) {
            var carInfoRequest = new RequestCarInfo { CarId = carId };
            _udp.Send(carInfoRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + carInfoRequest);
            }
        }

        public void BroadcastChatMessage(string msg) {
            var chatRequest = new RequestBroadcastChat { ChatMessage = msg };
            _udp.Send(chatRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + chatRequest);
            }
        }

        public void SendChatMessage(byte carId, string msg) {
            var chatRequest = new RequestSendChat { CarId = carId, ChatMessage = msg };
            _udp.Send(chatRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + chatRequest);
            }
        }

        [Localizable(false)]
        public void AdminCommand(string command) {
            var chatRequest = new RequestAdminCommand { Command = command };
            _udp.Send(chatRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + chatRequest);
            }
        }

        public void RestartSession() {
            var chatRequest = new RequestRestartSession();
            _udp.Send(chatRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + chatRequest);
            }
        }

        public void NextSession() {
            var chatRequest = new RequestNextSession();
            _udp.Send(chatRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + chatRequest);
            }
        }

        public void EnableRealtimeReport(TimeSpan interval) {
            _settings.RealtimeUpdateInterval = interval;
            _currentSession.RealtimeUpdateInterval = (ushort)interval.TotalMilliseconds;

            var enableRealtimeReportRequest = new RequestRealtimeInfo { Interval = (ushort)interval.TotalMilliseconds };
            _udp.Send(enableRealtimeReportRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + enableRealtimeReportRequest);
            }
        }

        /// <summary>
        /// Request a SessionInfo object, use -1 for the current session
        /// </summary>
        /// <param name="sessionIndex"></param>
        public void RequestSessionInfo(Int16 sessionIndex) {
            var sessionRequest = new RequestSessionInfo { SessionIndex = sessionIndex };
            _udp.Send(sessionRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + sessionRequest);
            }
        }

        public void RequestKickDriverById(byte carId) {
            var kickRequest = new RequestKickUser { CarId = carId };
            _udp.Send(kickRequest.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + kickRequest);
            }
        }

        public void RequestSetSession(RequestSetSession requestSetSession) {
            _udp.Send(requestSetSession.ToBinary());
            if (_settings.LogServerRequests) {
                Logging.Debug("Request: " + requestSetSession);
            }
        }

        public void BroadcastCspCommand<T>(T command) where T : struct, ICspCommand {
            BroadcastChatMessage(command.Serialize());
        }

        public void SendCspCommand<T>(byte carId, T command) where T : struct, ICspCommand {
            SendChatMessage(carId, command.Serialize());
        }

        public void BroadcastCspCommand(string commandSerialized) {
            BroadcastChatMessage(commandSerialized);
        }

        public void SendCspCommand(byte carId, string commandSerialized) {
            SendChatMessage(carId, commandSerialized);
        }
        #endregion

        #region some helper methods
        public static string FormatTimespan(int timespan) {
            var minutes = timespan / 1000 / 60;
            var seconds = (timespan - minutes * 1000 * 60) / 1000.0;
            return $"{minutes:00}:{seconds:00.000}";
        }

        private long GetLastLapTimestamp(DriverInfo driver) {
            var lapReport = _currentSession.Laps.FirstOrDefault(l => l.ConnectionId == driver.ConnectionId && l.LapNo == driver.LapCount);
            if (lapReport != null) {
                return lapReport.Timestamp;
            }
            return long.MaxValue;
        }
        #endregion

        public void Dispose() {
            if (IsConnected) {
                Disconnect();
            }
            foreach (var plugin in _plugins) {
                plugin.Dispose();
            }
        }
    }
}