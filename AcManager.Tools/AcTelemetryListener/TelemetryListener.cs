using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins.Helpers;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcTelemetryListener {
    public class TelemetryListenerSettings {
        public int ListeningPort { get; set; } = 9997;

        public int RemotePort { get; set; } = 9996;

        public string RemoteHostName { get; set; } = "127.0.0.1";

        public bool ConnectAutomatically { get; set; } = true;

        public TimeSpan AcServerKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(10d);
    }

    public class TelemetryListener : IDisposable {
        private TelemetryListenerSettings _settings;
        private DuplexUdpClient _udp;
        private readonly object _lockObject = new object();

        public TelemetryListener([NotNull] TelemetryListenerSettings settings) {
            _settings = settings;
            _udp = new DuplexUdpClient();

            if (settings.ConnectAutomatically) {
                ConnectDelay().Ignore();
            }
        }

        private async Task ConnectDelay() {
            await Task.Yield();
            if (!IsConnected) {
                Connect();
            }
        }

        public bool IsConnected => _udp.Opened;

        public void Connect() {
            Console.WriteLine("CONNECTING");
            lock (_lockObject) {
                if (IsConnected) {
                    throw new Exception("TelemetryListener already connected");
                }

                _udp.Open(_settings.ListeningPort, _settings.RemoteHostName, _settings.RemotePort, MessageReceived, msg => {
                    Console.WriteLine("ERROR: " + msg);
                    Logging.Warning(msg);
                });

                try {
                    Console.WriteLine("CONNECTED");
                    OnConnected();
                } catch (Exception ex) {
                    Logging.Warning(ex);
                }
            }
        }

        public DateTime LastServerActivity { get; private set; }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
        public struct HandshakeMessage {
            public int Identifier;
            public int Version;
            public int OperationID;

            public static readonly int Size = Marshal.SizeOf(typeof(HandshakeMessage));
            public static readonly byte[] Buffer = new byte[Size];

            public TimestampedBytes ToBinary() {
                return this.ToTimestampedBytes(Buffer);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
        public struct HandshakeResponse {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
            public string CarName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
            public string DriverName;

            public int Identifier;
            public int Version;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
            public string TrackName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
            public string TrackConfig;
        }

        public void SendHandshake() {
            var sessionRequest = new HandshakeMessage {
                Identifier = 1,
                Version = 1,
                OperationID = 0
            };
            _udp.Send(sessionRequest.ToBinary());
            Console.WriteLine("HANDSHAKE SENT");
        }

        private bool _waitingForHandshakeResponse;

        private void MessageReceived(TimestampedBytes data) {
            LastServerActivity = DateTime.Now;

            lock (_lockObject) {
                Console.WriteLine("MESSAGE RECEIVED: " + data.RawData.Length);
                if (_waitingForHandshakeResponse) {
                    _waitingForHandshakeResponse = false;
                    var msg = data.ToStruct<HandshakeResponse>();
                    Console.WriteLine("msg.CarName=" + msg.CarName);
                    Console.WriteLine("msg.DriverName=" + msg.DriverName);
                    Console.WriteLine("msg.TrackConfig=" + msg.TrackConfig);
                    Console.WriteLine("msg.TrackName=" + msg.TrackName);
                    Console.WriteLine("msg.Version=" + msg.Version);
                }
            }

            /*lock (_lockObject) {
                var msg = AcMessageParser.Parse(data);
            }*/
        }

        private void OnConnected() {
            try {
                // If we do not receive the session Info in the next 3 seconds request info (async).
                ThreadPool.QueueUserWorkItem(o => {
                    _waitingForHandshakeResponse = true;
                    SendHandshake();
                });

                // If the KeepAlive monitor is configured, we'll start a endless loop
                // that will try to determine a killed acServer.
                if (_settings.AcServerKeepAliveInterval > TimeSpan.Zero) {
                    ThreadPool.QueueUserWorkItem(o => {
                        while (true) {
                            // Sleeping for some seconds
                            Thread.Sleep(_settings.AcServerKeepAliveInterval);
                            if (!IsConnected) break;

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
                                // RequestSessionInfo(-1);

                                // Then we’ll give the server some time to answer this request, which
                                // should set the date.
                                Thread.Sleep(1500);

                                // Still late?
                                if (LastServerActivity + _settings.AcServerKeepAliveInterval < DateTime.Now) {
                                    // We’ll go to the “server is dead” state so we won’t repeat this until the next
                                    // “real” timeout.
                                    LastServerActivity = DateTime.MinValue;

                                    /*foreach (var plugin in _plugins) {
                                        try {
                                            plugin.OnAcServerTimeout();
                                        } catch (Exception ex) {
                                            Logging.Warning(ex);
                                        }
                                    }*/
                                }
                            }
                        }
                    });
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        public void Dispose() {
            try {
                if (IsConnected) {
                    _udp.Close();
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }
    }
}