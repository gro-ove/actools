using System;
using System.Globalization;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class StringTestEntry : ITestEntry {
        private readonly bool _wholeMatch, _strictMode;
        private readonly string _str;
        private readonly double? _strAsDouble;
        private readonly bool? _strAsBool;

        public StringTestEntry(string str, bool wholeMatch, bool strictMode) {
            _str = str.ToLowerInvariant();
            _wholeMatch = wholeMatch;
            _strictMode = strictMode;
            _strAsDouble = AsDouble(_str);
            _strAsBool = AsBool(_str);
        }

        internal static double? AsDouble(string s) {
            double d;
            return FlexibleParser.TryParseDouble(s, out d) ? d : (double?)null;
        }

        internal static bool? AsBool(string s) {
            switch (s) {
                case "true":
                case "yes":
                case "y":
                case "on":
                case "+":
                case "1":
                    return true;

                case "false":
                case "no":
                case "not":
                case "n":
                case "off":
                case "-":
                case "0":
                    return false;

                default:
                    return null;
            }
        }

        public override string ToString() {
            return "=" + _str;
        }

        public bool Test(string value) {
            if (value == null) return false;

            if (_strictMode) {
                return value.Equals(_str, StringComparison.OrdinalIgnoreCase);
            }

            if (_wholeMatch) {
                return value.StartsWith(_str, StringComparison.OrdinalIgnoreCase);
            }

            var i = value.IndexOf(_str, StringComparison.OrdinalIgnoreCase);
            switch (i) {
                case -1:
                    return false;

                case 0:
                    return true;

                default:
                    return Filter.OptionSimpleMatching || char.IsWhiteSpace(value[i - 1]);
            }
        }

        public bool Test(double value) {
            return _strAsDouble.HasValue && Math.Abs(_strAsDouble.Value - value) < 0.0001;
        }

        public bool Test(bool value) {
            return _strAsBool == value;
        }

        public bool Test(TimeSpan value) {
            return Test(value.ToString());
        }

        public bool Test(DateTime value) {
            return Test(value.ToString(CultureInfo.CurrentUICulture));
        }
    }
}