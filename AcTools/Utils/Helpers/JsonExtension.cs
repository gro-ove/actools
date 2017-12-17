using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.Utils.Helpers {
    public class JObjectRestorationScheme {
        public class Field {
            public readonly string Name, ParentName;
            public readonly FieldType Type;

            public Field([Localizable(false)] string name, FieldType type) {
                Name = name;
                Type = type;
            }

            public Field([Localizable(false)] string name, [Localizable(false)] string parentName, FieldType type) {
                Name = name;
                ParentName = parentName;
                Type = type;
            }

            public bool IsMultiline => Type == FieldType.StringMultiline || Type == FieldType.StringsArray ||
                    Type == FieldType.PairsArray;
        }

        public enum FieldType {
            String,
            NonNullString,
            StringMultiline,
            Number,
            Boolean,
            StringsArray,
            PairsArray
        }

        public readonly Field[] Fields;

        public JObjectRestorationScheme(params Field[] fields) {
            Fields = fields;
        }
    }

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

    public static class JsonExtension {
        public static void Populate([NotNull] this JToken value, [NotNull] object target){
            using (var sr = value.CreateReader()) {
                JsonSerializer.CreateDefault().Populate(sr, target);
            }
        }

        [NotNull]
        public static JObject Parse([NotNull, LocalizationRequired(false)] string data) {
            try {
                return JObject.Parse(data);
            } catch (Exception) {
                try {
                    return JObject.Parse(data.Replace("﻿", ""));
                } catch (Exception) {
                    AcToolsLogging.Write(data);
                    throw;
                }
            }
        }

        /* Example:
            [
                { ""$:references"": true, ""refkey2"": [ 8, 9, ""$:refkey"" ] },
                { ""$:references"": { ""refkey"": [ ""hahaha"" ] }, ""l"": [ 5, ""$:refkey2"" ] },
                { ""$:references"": { ""refkey"": [ 1, 2 ] }, ""s"": ""value"", ""l"": [ 5, ""$:refkey2"" ], ""o"": { ""k"": ""v"", ""ref"": ""$:refkey"" } },
            ]

           Unwraps to:
            [
                { "l": [ 5, [ 8, 9, [ "hahaha" ] ] ] },
                { "s": "value", "l": [ 5, [ 8, 9, [ 1, 2 ] ] ], "o": { "k": "v", "ref": [ 1, 2 ] } }
            ]

           TODO: Add proper documentation?
         */
        public static T UnwrapReferences<T>(T token) where T : JToken {
            return UnwrapReferences(token, s => null, out var u) ? (T)u : token;
        }

        private static bool UnwrapReferences(JToken token, Func<string, JToken> refCallback, out JToken unwrapped) {
            const string refKey = "$:references";
            var refCallbackLocal = refCallback;
            switch (token){
                case JObject o:
                    if (o[refKey] is JObject or){
                        o.Remove(refKey);
                        refCallbackLocal = k => or[k] ?? refCallback(k);
                    }

                    var keys = o.OfType<JProperty>().Select(x => x.Name).ToList();
                    foreach (var key in keys){
                        if (UnwrapReferences(o[key], refCallbackLocal, out var u)){
                            o[key] = u;
                        }
                    }
                    break;
                case JArray o:
                    if (o.Count > 0 && o[0] is JObject ar && ar[refKey]?.Type == JTokenType.Boolean && (bool)ar[refKey]){
                        o.Remove(ar);
                        refCallbackLocal = k => ar[k] ?? refCallback(k);
                    }

                    for (var i = 0; i < o.Count; i++){
                        if (UnwrapReferences(o[i], refCallbackLocal, out var u)){
                            o[i] = u;
                        }
                    }
                    break;
                case JToken s when s.Type == JTokenType.String:
                    var sv = s.ToString();
                    if (sv.StartsWith("$:")) {
                        var rf = refCallback(sv.Substring(2))?.DeepClone();
                        if (rf != null){
                            unwrapped = UnwrapReferences(rf, refCallback, out var ur) ? ur : rf;
                            return true;
                        }
                    }
                    break;
            }

            unwrapped = token;
            return false;
        }

        public static string GetStringValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key) {
            var value = obj[key];
            if (value == null || value.Type != JTokenType.String && value.Type != JTokenType.Integer &&
                    value.Type != JTokenType.Float) return null;
            var result = value.ToString();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        public static int? GetIntValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key) {
            var value = obj[key];
            if (value == null || value.Type != JTokenType.String && value.Type != JTokenType.Integer &&
                    value.Type != JTokenType.Float) return null;

            var result = value.ToString();
            if (string.IsNullOrEmpty(result)) return null;

            return !double.TryParse(result, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? (int?)null : (int)val;
        }

        public static int GetIntValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key, int defaultValue) {
            return obj.GetIntValueOnly(key) ?? defaultValue;
        }

        public static double? GetDoubleValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key) {
            var value = obj[key];
            if (value == null || value.Type != JTokenType.String && value.Type != JTokenType.Integer &&
                    value.Type != JTokenType.Float) return null;

            var result = value.ToString();
            if (string.IsNullOrEmpty(result)) return null;

            return !double.TryParse(result, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? (double?)null : val;
        }

        public static double GetDoubleValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key, double defaultValue) {
            return obj.GetDoubleValueOnly(key) ?? defaultValue;
        }

        public static bool? GetBoolValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key) {
            var value = obj[key];
            if (value == null || value.Type != JTokenType.Boolean && value.Type != JTokenType.Integer &&
                    value.Type != JTokenType.Float) return null;
            return (bool)value;
        }

        public static bool GetBoolValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key, bool defaultValue) {
            return obj.GetBoolValueOnly(key) ?? defaultValue;
        }

        public static GeoTagsEntry GetGeoTagsValueOnly([NotNull] this JToken obj, [LocalizationRequired(false)] string key) {
            if (!(obj[key] is JArray value) || value.Count != 2) return null;
            var lat = value[0];
            var lon = value[1];
            if (lat == null || lat.Type != JTokenType.String ||
                    lon == null || lon.Type != JTokenType.String) return null;
            return new GeoTagsEntry(lat.ToString(), lon.ToString());
        }

        public static JArray ToJObject(this GeoTagsEntry geoTagsEntry) {
            return new JArray(geoTagsEntry.Latitude, geoTagsEntry.Longitude);
        }

        private static Regex _dequoteStringRegex;

        private static string DequoteString([LocalizationRequired(false)] string s) {
            return (_dequoteStringRegex ?? (_dequoteStringRegex =
                    new Regex(@"^\s*['""]|['""]\s*$|\\(?="")", RegexOptions.Compiled))).Replace(s, "");
        }

        public static JObject TryToRestore([LocalizationRequired(false)] string damagedJson, JObjectRestorationScheme scheme) {
            var result = new JObject();

            var input = Regex.Replace(damagedJson, @"\r?\n|\n", "\n").Trim();
            foreach (var field in scheme.Fields) {
                var match = Regex.Match(input, $@"(?:""\s*{field.Name}\s*""|'\s*{field.Name}\s*'|{field.Name})\s*:\s*([\s\S]+)");
                if (!match.Success) continue;

                var value = match.Groups[1].Value.Trim();

                if (!field.IsMultiline) {
                    value = value.Split('\n')[0];
                    value = Regex.Replace(value, @"\s*,?\s*(""\s*\w+\s*""|'\s*\w+\s*'|\w+)\s*:[\s\S]+|\s*}", "");
                }

                value = Regex.Replace(value, @"(?:\n?\s*,?\s*(""\s*\w+\s*""|'\s*\w+\s*'|\w+)\s*:|\s*})[\s\S]*$", "");
                value = Regex.Replace(value.Trim(), @",$", "");

                JToken processedValue;

                if (value == "null") {
                    processedValue = null;
                } else {
                    switch (field.Type) {
                        case JObjectRestorationScheme.FieldType.String:
                        case JObjectRestorationScheme.FieldType.NonNullString:
                        case JObjectRestorationScheme.FieldType.StringMultiline:
                            processedValue = DequoteString(value);
                            break;

                        case JObjectRestorationScheme.FieldType.Number:
                            var doubleValue = FlexibleParser.ParseDouble(value);
                            if (Equals(doubleValue % 1.0, 0.0)) {
                                processedValue = (long)doubleValue;
                            } else {
                                processedValue = doubleValue;
                            }
                            break;

                        case JObjectRestorationScheme.FieldType.Boolean:
                            processedValue = Regex.IsMatch(value, @"\b(true|on|yes|1)\b", RegexOptions.IgnoreCase);
                            break;

                        case JObjectRestorationScheme.FieldType.StringsArray:
                            processedValue = new JArray(
                                    Regex.Split(value, @"^\s*\[|(?<!\\)""\s*,?\s*""|\s*(?:,\s*\n|\n\s*,?)\s*|\]\s*$")
                                         .Select(DequoteString)
                                         .Where(x => x.Length > 0 && x != "[" && x != "]")
                                         .Cast<object>().ToArray());
                            break;

                        case JObjectRestorationScheme.FieldType.PairsArray:
                            processedValue = new JArray(
                                    Regex.Split(value, @"^\s*\[|(?<!\\)""\s*\]?\s*,\s*\[??\s*""|\s*\]?\s*(?:,\s*\n|\n\s*,?)\s*\[?\s*|\]\s*$")
                                         .Select(DequoteString)
                                         .Where(x => x.Length > 0 && x != "[" && x != "]")
                                         .Partition(2)
                                         .Where(x => x.Length == 2)
                                         .Select(x => new JArray(x.ToArray<object>()))
                                         .Cast<object>().ToArray());
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (field.ParentName != null) {
                    if (!(result[field.ParentName] is JObject obj)) {
                        result[field.ParentName] = obj = new JObject();
                    }

                    obj[field.Name] = processedValue;
                } else {
                    result[field.Name] = processedValue;
                }
            }

            foreach (var field in scheme.Fields.Where(x => x.Type == JObjectRestorationScheme.FieldType.NonNullString && !result.TryGetValue(x.Name, out _))) {
                if (field.ParentName != null) {
                    if (!(result[field.ParentName] is JObject obj)) {
                        result[field.ParentName] = obj = new JObject();
                    }

                    obj[field.Name] = "";
                } else {
                    result[field.Name] = "";
                }
            }

            return result;
        }

        public static void SetNonDefault(this JObject obj, [LocalizationRequired(false)] string key, string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                obj.Remove(key);
            } else {
                obj[key] = value;
            }
        }

        public static void SetNonDefault(this JObject obj, [LocalizationRequired(false)] string key, int? value) {
            if (!value.HasValue || value == 0) {
                obj.Remove(key);
            } else {
                obj[key] = value.Value;
            }
        }

        public static void SetNonDefault(this JObject obj, [LocalizationRequired(false)] string key, bool? value) {
            if (!value.HasValue || value == false) {
                obj.Remove(key);
            } else {
                obj[key] = value.Value;
            }
        }
    }

    /// <summary>
    /// Deserialization without reflection.
    /// </summary>
    [LocalizationRequired(false)]
    public static class JsonDeserializationExtension {
        public static void MatchNext(this JsonTextReader reader, JsonToken token) {
            if (!reader.Read() || reader.TokenType != token) {
                throw new Exception(token + " expected");
            }
        }

        public static bool IsMatchNext(this JsonTextReader reader, JsonToken token) {
            return reader.Read() && reader.TokenType == token;
        }

        public static bool Until(this JsonTextReader reader, JsonToken token) {
            return reader.Read() && reader.TokenType != token;
        }

        public static string[] ReadStringArray(this JsonTextReader reader, int approximateSize = 10) {
            if (reader.TokenType != JsonToken.StartArray) {
                throw new Exception("StartArray expected");
            }

            var result = new List<string>(approximateSize);
            while (reader.Until(JsonToken.EndArray)) {
                if (reader.Value == null) throw new Exception("Value expected");
                result.Add(reader.Value.ToString());
            }

            return result.ToArray();
        }

        public static int[] ReadIntArray(this JsonTextReader reader, int approximateSize = 10) {
            if (reader.TokenType != JsonToken.StartArray) {
                throw new Exception("StartArray expected");
            }

            var result = new List<int>(approximateSize);
            while (reader.Until(JsonToken.EndArray)) {
                if (reader.Value == null) throw new Exception("Value expected");
                result.Add(int.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture));
            }

            return result.ToArray();
        }

        public static long[] ReadLongArray(this JsonTextReader reader, int approximateSize = 10) {
            if (reader.TokenType != JsonToken.StartArray) {
                throw new Exception("StartArray expected");
            }

            var result = new List<long>(approximateSize);
            while (reader.Until(JsonToken.EndArray)) {
                if (reader.Value == null) throw new Exception("Value expected");
                result.Add(long.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture));
            }

            return result.ToArray();
        }
    }

    /// <summary>
    /// Serialization without reflection.
    /// </summary>
    [LocalizationRequired(false)]
    public static class JsonSerializationExtension {
        public static void Write(this JsonTextWriter writer, string key, bool? value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void Write(this JsonTextWriter writer, string key, Enum value) {
            writer.WritePropertyName(key);
            writer.WriteValue(value);
        }

        public static void Write(this JsonTextWriter writer, string key, DateTime? value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void Write(this JsonTextWriter writer, string key, TimeSpan? value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void Write(this JsonTextWriter writer, string key, int? value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void Write(this JsonTextWriter writer, string key, double? value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void Write(this JsonTextWriter writer, string key, string value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteValue(value);
        }

        public static void Write(this JsonTextWriter writer, string key, string[] value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            for (var i = 0; i < value.Length; i++) {
                writer.WriteValue(value[i]);
            }
            writer.WriteEndArray();
        }

        public static void Write(this JsonTextWriter writer, string key, int[] value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            for (var i = 0; i < value.Length; i++) {
                writer.WriteValue(value[i].ToString(CultureInfo.InvariantCulture));
            }
            writer.WriteEndArray();
        }

        public static void Write(this JsonTextWriter writer, string key, double[] value) {
            if (value == null) return;
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            for (var i = 0; i < value.Length; i++) {
                writer.WriteValue(value[i].ToString(CultureInfo.InvariantCulture));
            }
            writer.WriteEndArray();
        }

        public static void WriteNonDefault(this JsonTextWriter writer, string key, bool? value) {
            if (!value.HasValue || Equals(value.Value, default(bool))) return;

            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void WriteNonDefault(this JsonTextWriter writer, string key, int? value) {
            if (!value.HasValue || Equals(value.Value, default(int))) return;

            writer.WritePropertyName(key);
            writer.WriteValue(value.Value);
        }

        public static void WriteNonDefault(this JsonTextWriter writer, string key, double? value, string format = null) {
            if (!value.HasValue || Equals(value.Value, default(double))) return;

            if (format == null) {
                writer.WritePropertyName(key);
                writer.WriteValue(value.Value);
            } else {
                var s = value.Value.ToString(format, CultureInfo.InvariantCulture);
                if (!Equals(double.Parse(s, CultureInfo.InvariantCulture), 0d)) {
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(s);
                }
            }
        }
    }
}
