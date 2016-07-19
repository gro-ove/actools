using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class IniFileSection : Dictionary<string, string> {
        [CanBeNull]
        internal string Commentary { get; private set; }

        [CanBeNull]
        internal Dictionary<string, string> Commentaries { get; private set; }

        public void SetCommentary([CanBeNull, LocalizationRequired(false)] string value) {
            Commentary = value;
        }

        public void SetCommentary([NotNull, LocalizationRequired(false)] string key, [CanBeNull, LocalizationRequired(false)] string value) {
            if (value == null) {
                RemoveCommentary(key);
                return;
            }

            if (Commentaries == null) Commentaries = new Dictionary<string, string>(1);
            Commentaries[key] = value;
        }

        public void RemoveCommentary([NotNull, LocalizationRequired(false)] string key) {
            if (Commentaries == null) return;
            Commentaries.Remove(key);
            if (Commentaries.Count == 0) {
                Commentaries = null;
            }
        }

        public new dynamic this[[NotNull, LocalizationRequired(false)] string key] {
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
        public string Get([NotNull, LocalizationRequired(false)] string key) {
            return ContainsKey(key) ? base[key] : null;
        }

        [CanBeNull]
        public string Get([NotNull, LocalizationRequired(false)] string key, [Localizable(false)] string defaultValue) {
            return ContainsKey(key) ? base[key] : defaultValue;
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public bool GetBool([NotNull, LocalizationRequired(false)] string key) {
            if (!ContainsKey(key)) throw new Exception("Value is missing!");
            var value = Get(key);
            return value == "1";
        }

        public bool GetBool([NotNull, LocalizationRequired(false)] string key, bool defaultValue) {
            if (!ContainsKey(key)) return defaultValue;
            var value = Get(key);
            return value == "1";
        }

        public bool? GetBoolNullable([NotNull, LocalizationRequired(false)] string key) {
            if (!ContainsKey(key)) return null;
            var value = Get(key);
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "y", StringComparison.OrdinalIgnoreCase);
        }

        [NotNull]
        public string[] GetStrings([NotNull, LocalizationRequired(false)] string key, char delimiter = ',') {
            return Get(key)?.Split(delimiter).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray() ?? new string[0];
        }

        [NotNull]
        public double[] GetVector3([NotNull, LocalizationRequired(false)] string key) {
            var result = GetStrings(key).Select(x => FlexibleParser.ParseDouble(x, 0d)).ToArray();
            return result.Length == 3 ? result : new double[3];
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public double GetDouble([NotNull, LocalizationRequired(false)] string key) {
            return FlexibleParser.ParseDouble(Get(key));
        }

        public double? GetDoubleNullable([NotNull, LocalizationRequired(false)] string key) {
            return FlexibleParser.TryParseDouble(Get(key));
        }

        public double GetDouble([NotNull, LocalizationRequired(false)] string key, double defaultValue) {
            return FlexibleParser.ParseDouble(Get(key), defaultValue);
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public int GetInt([NotNull, LocalizationRequired(false)] string key) {
            return FlexibleParser.ParseInt(Get(key));
        }

        public int? GetIntNullable([NotNull, LocalizationRequired(false)] string key) {
            return FlexibleParser.TryParseInt(Get(key));
        }

        public int GetInt([NotNull, LocalizationRequired(false)] string key, int defaultValue) {
            return FlexibleParser.TryParseInt(Get(key)) ?? defaultValue;
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public long GetLong([NotNull, LocalizationRequired(false)] string key) {
            return FlexibleParser.ParseLong(Get(key));
        }

        public long? GetLongNullable([NotNull, LocalizationRequired(false)] string key) {
            return FlexibleParser.TryParseLong(Get(key));
        }

        public long GetLong([NotNull, LocalizationRequired(false)] string key, int defaultValue) {
            return FlexibleParser.TryParseLong(Get(key)) ?? defaultValue;
        }

        /// <summary>
        /// Warning! Throws exception if value is missing!
        /// </summary>
        [Obsolete]
        public T GetEnum<T>([NotNull, LocalizationRequired(false)] string key, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            var value = Get(key);
            return (T)Enum.Parse(typeof(T), value ?? "", ignoreCase);
        }

        public T? GetEnumNullable<T>([NotNull, LocalizationRequired(false)] string key, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            T result;
            return Enum.TryParse(Get(key), ignoreCase, out result) ? result : (T?)null;
        }

        public T GetEnum<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            T result;
            return Enum.TryParse(Get(key), ignoreCase, out result) ? result : defaultValue;
        }

        public T GetIntEnum<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            var i = GetInt(key, Convert.ToInt32(defaultValue));
            return Enum.IsDefined(typeof(T), i) ? (T)Enum.ToObject(typeof(T), i) : defaultValue;
        }

        private static void Set([NotNull, LocalizationRequired(false)] string key, object value) {
            throw new Exception($"Type is not supported: {value?.GetType().ToString() ?? "null"} (key: “{key}”)");
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, [LocalizationRequired(false)] string value) {
            if (value == null) return;
            base[key] = value;
        }

        public void Set<T>([NotNull, LocalizationRequired(false)] string key, IEnumerable<T> value, char delimiter = ',') {
            if (value == null) return;
            Set(key, value.Select(x => x.ToInvariantString()).JoinToString(delimiter));
        }

        public void SetId([NotNull, LocalizationRequired(false)] string key, string value) {
            if (value == null) return;
            base[key] = value.ToLowerInvariant();
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, Enum value) {
            base[key] = Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
        }

        public void SetIntEnum([NotNull, LocalizationRequired(false)] string key, Enum value) {
            base[key] = Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, int value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, int? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, long value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, long? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, double value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, double? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, double value, string format) {
            base[key] = value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, double? value, string format) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, bool value) {
            base[key] = value ? "1" : "0";
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, bool? value) {
            if (!value.HasValue) return;
            base[key] = value.Value ? "1" : "0";
        }
    }
}