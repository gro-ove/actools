using System;
using System.Linq;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.AcPlugins {
    public class AcServerPluginManagerSettings {
        [JsonConstructor]
        private AcServerPluginManagerSettings() { }

        public AcServerPluginManagerSettings([NotNull] ServerPresetObject server) {
            ListeningPort = server.PluginUdpAddress?.Split(':', '/').ElementAtOrDefault(1).As<int?>()
                    ?? throw new Exception("Plugin UDP address is not set");
            RemotePort = server.PluginUdpPort ?? throw new Exception("Plugin UDP port is not set");
            ConnectAutomatically = server.IsRunning;

            Capacity = server.Capacity;
            AdminPassword = server.AdminPassword;
        }

        /// <summary>
        /// Gets or sets the port on which the plugin manager receives messages from the AC server.
        /// </summary>
        [JsonProperty("listeningPort")]
        public int ListeningPort { get; set; }

        /// <summary>
        /// Gets or sets the port of the AC server where requests should be send to.
        /// </summary>
        [JsonProperty("remotePort")]
        public int RemotePort { get; set; }

        [CanBeNull]
        [JsonProperty("adminPassword")]
        public string AdminPassword { get; set; }

        [JsonProperty("capacity")]
        public int Capacity { get; set; }

        [JsonIgnore]
        public bool ConnectAutomatically { get; set; }

        /// <summary>
        /// Gets or sets the hostname of the AC server.
        /// </summary>
        [JsonIgnore]
        public string RemoteHostName { get; set; } = "127.0.0.1";

        [JsonProperty("realtimeUpdateInterval")]
        public TimeSpan RealtimeUpdateInterval { get; set; } = TimeSpan.FromSeconds(0.1);

        [JsonProperty("newSessionStartDelay")]
        public TimeSpan NewSessionStartDelay { get; set; } = TimeSpan.FromSeconds(3d);

        /// <summary>
        /// Gets or sets whether requests to the AC server should be logged.
        /// </summary>
        [JsonProperty("logServerRequests")]
        public bool LogServerRequests { get; set; } = true;

        [JsonProperty("logServerErrors")]
        public bool LogServerErrors { get; set; } = true;

        /// <summary>
        /// Keep alive interval; if this is set to something > 0 the Plugin will
        /// monitor the server and send a ServerTimeout if it's missing
        /// </summary>
        [JsonProperty("acServerKeepAliveInterval")]
        public TimeSpan AcServerKeepAliveInterval { get; set; }

        public string Serialize() {
            return JsonConvert.SerializeObject(this);
        }

        public static AcServerPluginManagerSettings Deserialize(string data) {
            Logging.Debug(data);
            Logging.Debug("ListeningPort=" + JsonConvert.DeserializeObject<AcServerPluginManagerSettings>(data).ListeningPort);
            return JsonConvert.DeserializeObject<AcServerPluginManagerSettings>(data);
        }
    }
}