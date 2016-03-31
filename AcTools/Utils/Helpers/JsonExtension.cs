using System;
using System.CodeDom;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace AcTools.Utils.Helpers {
    public class JObjectRestorationScheme {
        public class Field {
            public readonly string Name, ParentName;
            public readonly FieldType Type;

            public Field(string name, FieldType type) {
                Name = name;
                Type = type;
            }

            public Field(string name, string parentName, FieldType type) {
                Name = name;
                ParentName = parentName;
                Type = type;
            }

            public bool IsMultiline => Type == FieldType.StringMultiline || Type == FieldType.StringsArray ||
                                       Type == FieldType.PairsArray;
        }

        public enum FieldType {
            String, StringMultiline,
            Number, Boolean,
            StringsArray, PairsArray
        }

        public readonly Field[] Fields;

        public JObjectRestorationScheme(params Field[] fields) {
            Fields = fields;
        }
    }

    public static class JsonExtension {
        public static JObject Parse(string data) {
            try {
                return JObject.Parse(data);
            } catch (Exception) {
                return JObject.Parse(data.Replace("﻿", ""));
            }
            
        }

        public static string GetStringValueOnly(this JToken obj, string key) {
            var value = obj[key];
            if (value == null || value.Type != JTokenType.String && value.Type != JTokenType.Integer && 
                value.Type != JTokenType.Float) return null;
            var result = value.ToString();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        public static GeoTagsEntry GetGeoTagsValueOnly(this JToken obj, string key) {
            var value = obj[key] as JArray;
            if (value == null || value.Count != 2) return null;
            var lat = value[0];
            var lon = value[1];
            if (lat == null || lat.Type != JTokenType.String || 
                lon == null || lon.Type != JTokenType.String) return null;
            return new GeoTagsEntry(lat.ToString(), lon.ToString());
        }

        public static JArray ToJObject(this GeoTagsEntry geoTagsEntry) {
            return new JArray(geoTagsEntry.Latitude, geoTagsEntry.Longitude);
        }

        public static int? GetIntValueOnly(this JToken obj, string key) {
            var value = obj[key];
            if (value == null || value.Type != JTokenType.String && value.Type != JTokenType.Integer && 
                value.Type != JTokenType.Float) return null;
            var result = value.ToString();
            if (string.IsNullOrEmpty(result)) return null;
            double val;
            if (!double.TryParse(result, out val)) return null;
            return (int)val;
        }

        private static Regex _dequoteStringRegex;
        private static string DequoteString(string s) {
            return (_dequoteStringRegex ?? (_dequoteStringRegex = 
                new Regex(@"^\s*['""]|['""]\s*$|\\(?="")", RegexOptions.Compiled)
            )).Replace(s, "");
        }

        public static JObject TryToRestore(string damagedJson, JObjectRestorationScheme scheme) {
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
                        case JObjectRestorationScheme.FieldType.StringMultiline:
                            processedValue = DequoteString(value);
                            break;

                        case JObjectRestorationScheme.FieldType.Number:
                            var doubleValue = FlexibleParser.ParseDouble(value);
                            if (Equals(doubleValue%1.0, 0.0)) {
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
                                    .Select(x => new JArray(x.Cast<object>().ToArray()))
                                    .Cast<object>().ToArray());
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (field.ParentName != null) {
                    var obj = result[field.ParentName] as JObject;
                    if (obj == null) {
                        result[field.ParentName] = obj = new JObject();
                    }

                    obj[field.Name] = processedValue;
                } else {
                    result[field.Name] = processedValue;
                }
            }

            return result;
        }
    }
}
