using System;
using System.Globalization;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    public enum StringMatchMode {
        IncludedWithin, StartsWith, CompleteMatch
    }

    public class StringTestEntry : ITestEntry {
        private readonly StringMatchMode _mode;
        private readonly string _str;
        private readonly double? _strAsDouble;
        private readonly bool? _strAsBool;

        public StringTestEntry(string str, StringMatchMode mode, bool caseInvariant) {
            _str = caseInvariant ? str.ToLowerInvariant() : str;
            _mode = mode;
            _strAsDouble = AsDouble(_str);
            _strAsBool = AsBool(_str);
        }

        private static double? AsDouble(string s) {
            return FlexibleParser.TryParseDouble(s, out var d) ? d : (double?)null;
        }

        private static bool? AsBool(string s) {
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

        public string UnderlyingValue => _str;

        public void Set(ITestEntryFactory factory) {}

        public bool Test(string value) {
            if (value == null) return false;

            switch (_mode) {
                case StringMatchMode.CompleteMatch:
                    return value.Equals(_str, StringComparison.OrdinalIgnoreCase);
                case StringMatchMode.StartsWith:
                    return value.StartsWith(_str, StringComparison.OrdinalIgnoreCase);
                case StringMatchMode.IncludedWithin:
                    var i = value.IndexOf(_str, StringComparison.OrdinalIgnoreCase);
                    switch (i) {
                        case -1:
                            return false;

                        case 0:
                            return true;

                        default:
                            return Filter.OptionSimpleMatching || char.IsWhiteSpace(value[i - 1]);
                    }
                default:
                    throw new ArgumentOutOfRangeException();
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