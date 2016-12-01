using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class MinoratingServerInformation : ServerInformation {
        [JsonProperty(PropertyName = "activity")]
        public int Activity { get; set; }

        [JsonProperty(PropertyName = "cleanliness")]
        public int Cleanliness { get; set; }

        [JsonProperty(PropertyName = "competition")]
        public int Competition { get; set; }

        // what’s that? I can’t even tell what type it is
        //[JsonProperty(PropertyName = "grades")]
        //public object Grades { get; set; }

        private const int AverageDataSize = 256000;
        private const int AverageServersCount = 250;
        private static bool _failed;

        public new static MinoratingServerInformation[] Deserialize(Stream stream) {
            if (_failed) {
                return DeserializeSafe(stream);
            }

            try {
                return DeserializeFast(stream);
            } catch (Exception e) {
                Logging.Warning(e);
                _failed = true;
                throw;
            }
        }

        private static MinoratingServerInformation[] DeserializeSafe(Stream stream) {
            using (var memory = new MemoryStream(AverageDataSize)) {
                stream.CopyTo(memory);
                memory.Seek(0, SeekOrigin.Begin);

                try {
                    return DeserializeFast(memory);
                } catch (Exception e) {
                    Logging.Warning(e);
                    memory.Seek(0, SeekOrigin.Begin);
                    return JsonConvert.DeserializeObject<MinoratingServerInformation[]>(memory.ReadAsString());
                }
            }
        }

        private static bool SetMinoratingToken(JsonTextReader reader, ref string currentProperty, MinoratingServerInformation entry) {
            switch (reader.TokenType) {
                case JsonToken.Integer:
                    switch (currentProperty) {
                        case "activity":
                            entry.Activity = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "cleanliness":
                            entry.Cleanliness = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                        case "competition":
                            entry.Competition = int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                            return true;
                    }
                    break;

                case JsonToken.Null:
                    switch (currentProperty) {
                        case "grades":
                            // entry.Grades = null;
                            return true;
                    }
                    break;
            }

            return false;
        }

        private static MinoratingServerInformation[] DeserializeFast(Stream stream) {
            var reader = new JsonTextReader(new StreamReader(stream));

            var response = new List<MinoratingServerInformation>(AverageServersCount);
            var currentProperty = string.Empty;

            reader.MatchNext(JsonToken.StartArray);
            while (reader.IsMatchNext(JsonToken.StartObject)) {
                var entry = new MinoratingServerInformation();
                while (reader.Until(JsonToken.EndObject)) {
                    if (!SetToken(reader, ref currentProperty, entry)) {
                        SetMinoratingToken(reader, ref currentProperty, entry);
                    }
                }

                response.Add(entry);
            }

            return response.ToArray();
        }
    }
}