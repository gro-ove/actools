using System;
using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class FlexibleParser {
        private static Regex _parseDouble, _parseInt;

        [ContractAnnotation("s:null => false")]
        public static bool TryParseDouble([CanBeNull] string s, out double value) {
            if (s == null) {
                value = 0d;
                return false;
            }

            if (s.IndexOf(',') != -1) {
                s = s.Replace(',', '.');
            }

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return true;
            }

            if (_parseDouble == null) {
                _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?", RegexOptions.Compiled);
            }

            var match = _parseDouble.Match(s);
            if (match.Success) {
                return double.TryParse(match.Value.Replace(',', '.').Replace(" ", ""), NumberStyles.Any,
                                       CultureInfo.InvariantCulture, out value);
            }

            value = 0.0;
            return false;
        }

        [ContractAnnotation("s:null => null")]
        public static double? TryParseDouble(string s) {
            return TryParseDouble(s, out var result) ? result : (double?)null;
        }

        [ContractAnnotation("s:null => false")]
        public static bool TryParseInt([CanBeNull] string s, out int value) {
            if (s == null) {
                value = 0;
                return false;
            }

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) ||
                    int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return true;
            }

            if (_parseInt == null) {
                _parseInt = new Regex(@"-? *\d+");
            }

            var match = _parseInt.Match(s);
            if (match.Success) {
                return int.TryParse(match.Value.Replace(" ", ""), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out value);
            }

            value = 0;
            return false;
        }

        [ContractAnnotation("s:null => null")]
        public static int? TryParseInt(string s) {
            return TryParseInt(s, out var result) ? result : (int?)null;
        }

        [ContractAnnotation("s:null => false")]
        public static bool TryParseLong(string s, out long value) {
            if (_parseInt == null) {
                _parseInt = new Regex(@"-? *\d+");
            }

            if (s != null) {
                var match = _parseInt.Match(s);
                if (match.Success) {
                    return long.TryParse(match.Value.Replace(" ", ""), NumberStyles.Any,
                                        CultureInfo.InvariantCulture, out value);
                }
            }

            value = 0;
            return false;
        }

        [ContractAnnotation("s:null => null")]
        public static long? TryParseLong(string s) {
            return TryParseLong(s, out var result) ? result : (long?)null;
        }

        /// <summary>
        /// Throws an exception if can’t parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static double ParseDouble(string s) {
            if (!TryParseDouble(s, out var result)) {
                throw new FormatException();
            }

            return result;
        }

        public static double ParseDouble(string s, double defaultValue) {
            return TryParseDouble(s, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Throws an exception if can’t parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int ParseInt(string s) {
            if (!TryParseInt(s, out var result)) {
                throw new FormatException();
            }

            return result;
        }

        public static int ParseInt(string s, int defaultValue) {
            return TryParseInt(s, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Throws an exception if can’t parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static long ParseLong(string s) {
            if (!TryParseLong(s, out var result)) {
                throw new FormatException();
            }

            return result;
        }

        public static long ParseLong(string s, long defaultValue) {
            return TryParseLong(s, out var result) ? result : defaultValue;
        }

        #region Time
        /// <summary>
        /// Parse value from “12:34” to seconds from “00:00”
        /// </summary>
        /// <param name="value">Value in “12:34” (or “12:34:56”) format.</param>
        /// <param name="totalSeconds">Seconds from “00:00”.</param>
        /// <returns></returns>
        public static bool TryParseTime([CanBeNull] string value, out int totalSeconds) {
            if (value == null) {
                totalSeconds = 0;
                return false;
            }

            var splitted = value.Split(':');
            if (splitted.Length == 1) {
                if (TryParseInt(splitted[0], out var hours)) {
                    totalSeconds = hours * 60 * 60;
                    AmPmFix(ref totalSeconds);
                    return true;
                }
            } else if (splitted.Length == 2) {
                if (TryParseInt(splitted[0], out var hours) && TryParseInt(splitted[1], out var minutes)) {
                    totalSeconds = hours * 60 * 60 + minutes * 60;
                    AmPmFix(ref totalSeconds);
                    return true;
                }
            } else if (splitted.Length == 3) {
                if (TryParseInt(splitted[0], out var hours) && TryParseInt(splitted[1], out var minutes) && TryParseInt(splitted[2], out var seconds)) {
                    totalSeconds = hours * 60 * 60 + minutes * 60 + seconds;
                    AmPmFix(ref totalSeconds);
                    return true;
                }
            }

            totalSeconds = 0;
            return false;

            void AmPmFix(ref int d) {
                var lower = value.ToLowerInvariant();
                if (lower.Contains("am")) {
                    if (d >= 12 * 60 * 60) {
                        d -= 12 * 60 * 60;
                    }
                } else if (lower.Contains("pm")) {
                    if (d < 12 * 60 * 60) {
                        d += 12 * 60 * 60;
                    }
                }
            }
        }

        public static int? TryParseTime(string s) {
            return TryParseTime(s, out var result) ? result : (int?)null;
        }

        /// <summary>
        /// Throws an exception if can’t parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int ParseTime(string s) {
            if (!TryParseTime(s, out var result)) {
                throw new FormatException();
            }

            return result;
        }

        public static int ParseTime(string s, int defaultValue) {
            return TryParseTime(s, out var result) ? result : defaultValue;
        }
        #endregion

        #region Boolean
        public static bool TryParseBool(string s, out bool value) {
            if (s == null) {
                value = default;
                return false;
            }

            if (s == "1" ||
                    string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "ok", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "y", StringComparison.OrdinalIgnoreCase)) {
                value = true;
                return true;
            }

            if (s == "0" ||
                    string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "none", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "no", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "not", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "n", StringComparison.OrdinalIgnoreCase)) {
                value = false;
                return true;
            }

            value = default;
            return false;
        }

        public static bool? TryParseBool(string s) {
            return TryParseBool(s, out var result) ? result : (bool?)null;
        }

        /// <summary>
        /// Throws an exception if can’t parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool ParseBool(string s) {
            if (!TryParseBool(s, out var result)) {
                throw new FormatException();
            }

            return result;
        }

        public static bool ParseBool(string s, bool defaultValue) {
            return TryParseBool(s, out var result) ? result : defaultValue;
        }
        #endregion
    }
}
