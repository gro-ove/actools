using System;
using System.ComponentModel;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    [Localizable(false)]
    public class ServerInformation : IWithId {
        [JsonIgnore]
        private string _id;

        [JsonIgnore]
        public string Id => _id ?? (_id = Ip + ":" + PortHttp);

        [Obsolete]
        public string GetUniqueId() {
            return Id;
        }

        [NotNull, JsonProperty(PropertyName = "ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// For json-requests directly to launcher server.
        /// </summary>
        [JsonProperty(PropertyName = "cport")]
        public int PortHttp { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Creates new partially entry (will require more data loading later).
        /// </summary>
        /// <param name="address">Should be in format [IP]:[HTTP port].</param>
        /// <returns></returns>
        [CanBeNull]
        public static ServerInformation FromAddress(string address) {
            string ip;
            int port;
            return KunosApiProvider.ParseAddress(address, out ip, out port) && port > 0 ? new ServerInformation {
                Ip = ip,
                PortHttp = port
            } : null;
        }

        /// <summary>
        /// Creates new partially entry (will require more data loading later).
        /// </summary>
        /// <param name="description">Should be in format [IP]:[HTTP port];[Name].</param>
        /// <returns></returns>
        [CanBeNull]
        public static ServerInformation FromDescription(string description) {
            var splitted = description.Split(new[] { ';' }, 2);
            var result = FromAddress(splitted[0]);

            if (result != null && !string.IsNullOrWhiteSpace(splitted.ArrayElementAtOrDefault(1))) {
                result.Name = splitted[1].Trim();
            }

            return result;
        }

        public string ToDescription() {
            return Name == null ? Id : $@"{Id};{Name}";
        }
    }
}
