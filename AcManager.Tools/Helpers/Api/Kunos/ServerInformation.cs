using System.ComponentModel;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    [Localizable(false)]
    public class ServerInformation {
        public string GetUniqueId() {
            return Ip + ":" + Port;
        }

        [NotNull, JsonProperty(PropertyName = "ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// As a query argument for //aclobby1.grecian.net/lobby.ashx/…
        /// </summary>
        [JsonProperty(PropertyName = "port")]
        public int Port { get; set; }

        /// <summary>
        /// For json-requests directly to launcher server
        /// </summary>
        [JsonProperty(PropertyName = "cport")]
        public int PortC { get; set; }

        /// <summary>
        /// For race.ini & acs.exe
        /// </summary>
        [JsonProperty(PropertyName = "tport")]
        public int PortT { get; set; }

        [NotNull, JsonProperty(PropertyName = "name")]
        public string Name { get; set; } = "";

        [JsonProperty(PropertyName = "clients")]
        public int Clients { get; set; }

        [JsonProperty(PropertyName = "maxclients")]
        public int Capacity { get; set; }

        [NotNull, JsonProperty(PropertyName = "track")]
        public string TrackId { get; set; } = "";

        [NotNull, JsonProperty(PropertyName = "cars")]
        public string[] CarIds { get; set; } = { };

        [JsonProperty(PropertyName = "timeofday")]
        public int Time { get; set; }

        [JsonProperty(PropertyName = "session")]
        public int Session { get; set; }

        [NotNull, JsonProperty(PropertyName = "sessiontypes")]
        public int[] SessionTypes { get; set; } = { };

        [NotNull, JsonProperty(PropertyName = "durations")]
        public long[] Durations { get; set; } = { };

        [JsonProperty(PropertyName = "timeleft")]
        public long TimeLeft { get; set; }

        [NotNull, JsonProperty(PropertyName = "country")]
        public string[] Country { get; set; } = { "", "" };

        [JsonProperty(PropertyName = "pass")]
        public bool Password { get; set; }

        [JsonProperty(PropertyName = "pickup")]
        public bool PickUp { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty(PropertyName = "lastupdate")]
        public long LastUpdate { get; set; }

        [JsonProperty(PropertyName = "l")]
        public bool L { get; set; }

        [JsonIgnore]
        public bool IsLan { get; set; }
    }
}
