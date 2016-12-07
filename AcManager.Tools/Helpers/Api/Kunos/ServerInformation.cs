using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    [Localizable(false)]
    public class ServerInformation {
        public string GetUniqueId() {
            return Ip + ":" + PortHttp;
        }

        [NotNull, JsonProperty(PropertyName = "ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// For json-requests directly to launcher server.
        /// </summary>
        [JsonProperty(PropertyName = "cport")]
        public int PortHttp { get; set; }

        /// <summary>
        /// As a query argument for //aclobby1.grecian.net/lobby.ashx/….
        /// </summary>
        [JsonProperty(PropertyName = "port")]
        public int Port { get; set; }

        /// <summary>
        /// For race.ini & acs.exe.
        /// </summary>
        [JsonProperty(PropertyName = "tport")]
        public int PortRace { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "clients")]
        public int Clients { get; set; }

        [JsonProperty(PropertyName = "maxclients")]
        public int Capacity { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "track")]
        public string TrackId { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "cars")]
        public string[] CarIds { get; set; }

        [JsonProperty(PropertyName = "timeofday")]
        public int Time { get; set; }

        [JsonProperty(PropertyName = "session")]
        public int Session { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "sessiontypes")]
        public int[] SessionTypes { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "durations")]
        public long[] Durations { get; set; }

        [JsonProperty(PropertyName = "timeleft")]
        public long TimeLeft { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "country")]
        public string[] Country { get; set; }

        [JsonProperty(PropertyName = "pass")]
        public bool Password { get; set; }

        [JsonProperty(PropertyName = "pickup")]
        public bool PickUp { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty(PropertyName = "lastupdate")]
        public long LastUpdate { get; set; }

        [JsonProperty(PropertyName = "l"), Obsolete]
        public bool Lan { get; set; }

        [JsonIgnore]
        public bool IsLan { get; set; }

        [JsonIgnore]
        public bool LoadedDirectly { get; set; }

        [JsonIgnore]
        public bool IsFullyLoaded => Name != null && CarIds != null && TrackId != null;

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

        private const int AverageDataSize = 819200;
        private const int AverageServersCount = 1200;
        private static bool _failed;

        public static ServerInformation[] Deserialize(Stream stream) {
            // if parsing failed before, let’s do it the other way
            if (_failed) {
                return DeserializeSafe(stream);
            }

            try {
                return DeserializeFast(stream);
            } catch (Exception e) {
                Logging.Warning(e);

                // this bit won’t work because stream already was read and I don’t think it can be reset :(
                // return JsonConvert.DeserializeObject<ServerInformation[]>(stream.ReadAsString());

                // so let’s just make a note about it (so next time we’ll do parsing the other way)
                // and gracefully crash
                _failed = true;
                throw;
            }
        }

        private static ServerInformation[] DeserializeSafe(Stream stream) {
            // this is usually a pretty huge list
            using (var memory = new MemoryStream(AverageDataSize)) {
                stream.CopyTo(memory);
                memory.Seek(0, SeekOrigin.Begin);

                try {
                    return DeserializeFast(memory);
                } catch (Exception e) {
                    Logging.Warning(e);
                    memory.Seek(0, SeekOrigin.Begin);
                    return JsonConvert.DeserializeObject<ServerInformation[]>(memory.ReadAsString());
                }
            }
        }

        protected static bool SetToken(JsonTextReader reader, ref string currentProperty, ServerInformation entry) {
            switch (reader.TokenType) {
                case JsonToken.PropertyName:
                    currentProperty = reader.Value.ToString();
                    return true;

                case JsonToken.String:
                    switch (currentProperty) {
                        case "ip":
                            entry.Ip = reader.Value.ToString();
                            return true;
                        case "name":
                            entry.Name = reader.Value.ToString();
                            return true;
                        case "track":
                            entry.TrackId = reader.Value.ToString();
                            return true;
                    }
                    break;

                case JsonToken.Integer:
                    switch (currentProperty) {
                        case "port":
                            entry.Port = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "cport":
                            entry.PortHttp = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "tport":
                            entry.PortRace = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "clients":
                            entry.Clients = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "maxclients":
                            entry.Capacity = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "timeofday":
                            entry.Time = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "session":
                            entry.Session = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "timeleft":
                            entry.TimeLeft = long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "timestamp":
                            entry.Timestamp = long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "lastupdate":
                            entry.LastUpdate = long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                    }
                    break;

                case JsonToken.Boolean:
                    switch (currentProperty) {
                        case "pass":
                            entry.Password = bool.Parse(reader.Value.ToString());
                            return true;
                        case "pickup":
                            entry.PickUp = bool.Parse(reader.Value.ToString());
                            return true;
                        case "l":
                            entry.Lan = bool.Parse(reader.Value.ToString());
                            return true;
                    }
                    break;

                case JsonToken.StartArray:
                    switch (currentProperty) {
                        case "cars":
                            entry.CarIds = reader.ReadStringArray(1);
                            return true;
                        case "sessiontypes":
                            entry.SessionTypes = reader.ReadIntArray(1);
                            return true;
                        case "durations":
                            entry.Durations = reader.ReadLongArray(1);
                            return true;
                        case "country":
                            entry.Country = reader.ReadStringArray(2);
                            return true;
                        default:
                            while (reader.Until(JsonToken.EndArray)) { }
                            return true;
                    }

                case JsonToken.Null:
                    break;

                default:
                    throw new Exception("Unexpected token: " + reader.TokenType);
            }

            return false;
        }

        private static ServerInformation[] DeserializeFast(Stream stream) {
            var reader = new JsonTextReader(new StreamReader(stream));

            var response = new List<ServerInformation>(AverageServersCount);
            var currentProperty = string.Empty;

            reader.MatchNext(JsonToken.StartArray);
            while (reader.IsMatchNext(JsonToken.StartObject)) {
                var entry = new ServerInformation();
                while (reader.Until(JsonToken.EndObject)) {
                    SetToken(reader, ref currentProperty, entry);
                }

                response.Add(entry);
            }

            return response.ToArray();
        }
    }
}
