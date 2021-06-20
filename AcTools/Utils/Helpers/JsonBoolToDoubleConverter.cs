using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.Utils.Helpers {
    public class JsonBoolToDoubleConverter : JsonConverter {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            JToken.FromObject(value, serializer).WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            return reader.TokenType == JsonToken.Boolean ? ((bool)reader.Value ? 1d : 0d) : FlexibleParser.TryParseDouble(reader.Value?.ToString()) ?? 0d;
        }

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(double);
        }
    }
}