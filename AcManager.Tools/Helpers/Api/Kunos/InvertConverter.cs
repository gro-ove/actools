using System;
using System.Globalization;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class InvertConverter : JsonConverter {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer){
            writer.WriteValue((int)value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer){
            if (reader.Value is bool) return (bool)reader.Value ? -1 : 0;
            return int.TryParse(reader.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0;
        }

        public override bool CanConvert(Type objectType){
            return objectType == typeof(bool) || objectType == typeof(int);
        }
    }
}