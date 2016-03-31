using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AcTools.Utils.Helpers {
    public static class FlexibleParser {
        private static Regex _parseDouble, _parseInt;

        public static bool TryParseDouble(string s, out double value) {
            if (_parseDouble == null) {
                _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?");
            }

            if (s != null) {
                var match = _parseDouble.Match(s);
                if (match.Success) {
                    return double.TryParse(match.Value.Replace(',', '.').Replace(" ", ""), NumberStyles.Any,
                                           CultureInfo.InvariantCulture, out value);
                }
            }

            value = 0.0;
            return false;
        }

        public static double? TryParseDouble(string s) {
            double result;
            return TryParseDouble(s, out result) ? result : (double?) null;
        }

        public static bool TryParseInt(string s, out int value) {
            if (_parseInt == null) {
                _parseInt = new Regex(@"-? *\d+");
            }

            if (s != null) {
                var match = _parseInt.Match(s);
                if (match.Success) {
                    return int.TryParse(match.Value.Replace(" ", ""), NumberStyles.Any,
                                        CultureInfo.InvariantCulture, out value);
                }
            }

            value = 0;
            return false;
        }

        public static int? TryParseInt(string s) {
            int result;
            return TryParseInt(s, out result) ? result : (int?)null;
        }

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

        public static long? TryParseLong(string s) {
            long result;
            return TryParseLong(s, out result) ? result : (long?)null;
        }

        /// <summary>
        /// Parse value from “12:34” to seconds from “00:00”
        /// </summary>
        /// <param name="value">Value in “12:34” (or “12:34:56”) format.</param>
        /// <param name="totalSeconds">Seconds from “00:00”.</param>
        /// <returns></returns>
        public static bool TryParseTime(string value, out int totalSeconds) {
            var splitted = value.Split(':');
            if (splitted.Length == 2) {
                int hours, minutes;

                if (TryParseInt(splitted[0], out hours) && TryParseInt(splitted[1], out minutes)) {
                    totalSeconds = hours*60*60 + minutes*60;
                    return true;
                }
            } else if (splitted.Length == 3) {
                int hours, minutes, seconds;

                if (TryParseInt(splitted[0], out hours) && TryParseInt(splitted[1], out minutes) && TryParseInt(splitted[2], out seconds)) {
                    totalSeconds = hours*60*60 + minutes*60 + seconds;
                    return true;
                }
            }

            totalSeconds = 0;
            return false;
        }

        public static string ReplaceDouble(string s, double value) {
            if (_parseDouble == null) {
                _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?");
            }

            var match = _parseDouble.Match(s);
            if (!match.Success) return s;

            return s.Substring(0, match.Index) + value.ToString(CultureInfo.InvariantCulture) +
                   s.Substring(match.Index + match.Length);
        }

        /// <summary>
        /// Throws an exception if can't parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static double ParseDouble(string s) {
            double result;
            if (!TryParseDouble(s, out result)) {
                throw new FormatException();
            }

            return result;
        }

        public static double ParseDouble(string s, double defaultValue) {
            double result;
            return TryParseDouble(s, out result) ? result : defaultValue;
        }

        /// <summary>
        /// Throws an exception if can't parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int ParseInt(string s) {
            int result;
            if (!TryParseInt(s, out result)) {
                throw new FormatException();
            }

            return result;
        }

        public static int ParseInt(string s, int defaultValue) {
            int result;
            return TryParseInt(s, out result) ? result : defaultValue;
        }

        /// <summary>
        /// Throws an exception if can't parse!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static long ParseLong(string s) {
            long result;
            if (!TryParseLong(s, out result)) {
                throw new FormatException();
            }

            return result;
        }

        public static long ParseLong(string s, long defaultValue) {
            long result;
            return TryParseLong(s, out result) ? result : defaultValue;
        }
    }
}
