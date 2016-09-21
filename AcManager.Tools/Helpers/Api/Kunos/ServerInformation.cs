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

        public static ServerInformation[] DeserializeSafe(Stream stream) {
            try {
                return Deserialize(stream);
            } catch (Exception e) {
                Logging.Warning(e);
                return JsonConvert.DeserializeObject<ServerInformation[]>(stream.ReadAsString());
            }
        }

        public static ServerInformation[] Deserialize(Stream stream) {
            var reader = new JsonTextReader(new StreamReader(stream));

            var response = new List<ServerInformation>(1200);
            var currentProperty = string.Empty;

            reader.MatchNext(JsonToken.StartArray);
            while (reader.IsMatchNext(JsonToken.StartObject)) {
                var entry = new ServerInformation();

                while (reader.Until(JsonToken.EndObject)) {
                    switch (reader.TokenType) {
                        case JsonToken.PropertyName:
                            currentProperty = reader.Value.ToString();
                            break;

                        case JsonToken.String:
                            switch (currentProperty) {
                                case "ip":
                                    entry.Ip = reader.Value.ToString();
                                    break;
                                case "name":
                                    entry.Name = reader.Value.ToString();
                                    break;
                                case "track":
                                    entry.TrackId = reader.Value.ToString();
                                    break;
                            }
                            break;

                        case JsonToken.Integer:
                            switch (currentProperty) {
                                case "port":
                                    entry.Port = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "cport":
                                    entry.PortC = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "tport":
                                    entry.PortT = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "clients":
                                    entry.Clients = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "maxclients":
                                    entry.Capacity = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "timeofday":
                                    entry.Time = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "session":
                                    entry.Session = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "timeleft":
                                    entry.TimeLeft = long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "timestamp":
                                    entry.Timestamp = long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                                case "lastupdate":
                                    entry.LastUpdate = long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    break;
                            }
                            break;

                        case JsonToken.Boolean:
                            switch (currentProperty) {
                                case "pass":
                                    entry.Password = bool.Parse(reader.Value.ToString());
                                    break;
                                case "pickup":
                                    entry.PickUp = bool.Parse(reader.Value.ToString());
                                    break;
                                case "l":
                                    entry.L = bool.Parse(reader.Value.ToString());
                                    break;
                            }
                            break;

                        case JsonToken.StartArray:
                            switch (currentProperty) {
                                case "cars":
                                    entry.CarIds = reader.ReadStringArray(1);
                                    break;
                                case "sessiontypes":
                                    entry.SessionTypes = reader.ReadIntArray(1);
                                    break;
                                case "durations":
                                    entry.Durations = reader.ReadLongArray(1);
                                    break;
                                case "country":
                                    entry.Country = reader.ReadStringArray(2);
                                    break;
                                default:
                                    while (reader.Until(JsonToken.EndArray)) { }
                                    break;
                            }
                            break;

                        default:
                            throw new Exception("Unexpected token: " + reader.TokenType);
                    }
                }

                response.Add(entry);
            }

            return response.ToArray();
        }
    }
}
