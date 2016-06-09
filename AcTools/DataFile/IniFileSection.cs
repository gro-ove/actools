using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class IniFileSection : Dictionary<string, string> {
        public new dynamic this[string key] {
            get { return ContainsKey(key) ? base[key] : null; }
            set {
                if (value == null) {
                    Set(key, (string)null);
                } else {
                    Set(key, value);
                }
            }
        }

        [CanBeNull]
        public string Get(string key) {
            return ContainsKey(key) ? base[key] : null;
        }

        [CanBeNull]
        public string Get(string key, string defaultValue) {
            return ContainsKey(key) ? base[key] : defaultValue;
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public bool GetBool(string key) {
            if (!ContainsKey(key)) throw new Exception("Value is missing!");
            var value = Get(key);
            return value == "1";
        }

        public bool GetBool(string key, bool defaultValue) {
            if (!ContainsKey(key)) return defaultValue;
            var value = Get(key);
            return value == "1";
        }

        public bool? GetBoolNullable(string key) {
            if (!ContainsKey(key)) return null;
            var value = Get(key);
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "y", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<string> GetStrings(string key) {
            return Get(key)?.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0) ?? new string[0];
        }
        
        public double[] GetVector3(string key) {
            var result = GetStrings(key).Select(x => FlexibleParser.ParseDouble(x, 0d)).ToArray();
            return result.Length == 3 ? result : new double[3];
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public double GetDouble(string key) {
            return FlexibleParser.ParseDouble(Get(key));
        }

        public double? GetDoubleNullable(string key) {
            return FlexibleParser.TryParseDouble(Get(key));
        }

        public double GetDouble(string key, double defaultValue) {
            return FlexibleParser.ParseDouble(Get(key), defaultValue);
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public int GetInt(string key) {
            return FlexibleParser.ParseInt(Get(key));
        }

        public int? GetIntNullable(string key) {
            return FlexibleParser.TryParseInt(Get(key));
        }

        public int GetInt(string key, int defaultValue) {
            return FlexibleParser.TryParseInt(Get(key)) ?? defaultValue;
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public long GetLong(string key) {
            return FlexibleParser.ParseLong(Get(key));
        }

        public long? GetLongNullable(string key) {
            return FlexibleParser.TryParseLong(Get(key));
        }

        public long GetLong(string key, int defaultValue) {
            return FlexibleParser.TryParseLong(Get(key)) ?? defaultValue;
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public T GetEnum<T>(string key, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            var value = Get(key);
            return (T)Enum.Parse(typeof(T), value ?? "", ignoreCase);
        }

        public T? GetEnumNullable<T>(string key, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            T result;
            return Enum.TryParse(Get(key), ignoreCase, out result) ? result : (T?)null;
        }

        public T GetEnum<T>(string key, T defaultValue, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            T result;
            return Enum.TryParse(Get(key), ignoreCase, out result) ? result : defaultValue;
        }

        private static void Set(string key, object value) {
            throw new Exception($"Type is not supported: {value?.GetType().ToString() ?? "null"} (key: “{key}”)");
        }

        public void Set(string key, string value) {
            if (value == null) return;
            base[key] = value;
        }

        public void Set<T>(string key, IEnumerable<T> value) {
            if (value == null) return;
            Set(key, value.Select(x => x.ToInvariantString()).JoinToString(','));
        }

        public void SetId(string key, string value) {
            if (value == null) return;
            base[key] = value.ToLowerInvariant();
        }

        public void Set(string key, Enum value) {
            base[key] = Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, int value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, int? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, long value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, long? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, double value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, double? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, double value, string format) {
            base[key] = value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Set(string key, double? value, string format) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Set(string key, bool value) {
            base[key] = value ? "1" : "0";
        }

        public void Set(string key, bool? value) {
            if (!value.HasValue) return;
            base[key] = value.Value ? "1" : "0";
        }
    }
}