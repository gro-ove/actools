using System;
using System.Globalization;
using JetBrains.Annotations;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class FileSizeTestEntry : ITestEntry {
        private readonly Operator _op;
        private readonly double _metersValue;

        public override string ToString() {
            return (_op == Operator.Less ? "<" : _op == Operator.Equal ? "=" : ">") + _metersValue.ToString(CultureInfo.InvariantCulture);
        }

        internal FileSizeTestEntry(Operator op, double metersValue) {
            _op = op;
            _metersValue = metersValue;
        }

        public static bool IsDistanceKey(string key) {
            return string.Equals(key, "size", StringComparison.Ordinal);
        }

        public static bool IsDistanceValue(string value) {
            return value.IndexOf("kb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("mb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("gb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("кб", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("мб", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("гб", StringComparison.InvariantCultureIgnoreCase) != -1;
        }

        public static bool ToBytes([CanBeNull] string value, out double meters) {
            if (value == null) {
                meters = 0d;
                return false;
            }

            if (!FlexibleParser.TryParseDouble(value, out meters)) {
                return false;
            }

            if (value.IndexOf("kb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("кб", StringComparison.InvariantCultureIgnoreCase) != -1) {
                meters *= 1e3;
            } else if (value.IndexOf("mb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("мб", StringComparison.InvariantCultureIgnoreCase) != -1) {
                meters *= 1e6;
            } else if (value.IndexOf("gb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                    value.IndexOf("гб", StringComparison.InvariantCultureIgnoreCase) != -1) {
                meters *= 1e9;
            }

            return true;
        }

        public bool Test(string value) {
            double val;
            return ToBytes(value, out val) && Test(val);
        }

        public bool Test(double value) {
            switch (_op) {
                case Operator.Less:
                    return value < _metersValue;

                case Operator.LessEqual:
                    return value <= _metersValue;

                case Operator.More:
                    return value > _metersValue;

                case Operator.MoreEqual:
                    return value >= _metersValue;

                case Operator.Equal:
                    return Math.Abs(value - _metersValue) < 0.0001;

                default:
                    return false;
            }
        }

        public bool Test(bool value) {
            return Test(value ? 1.0 : 0.0);
        }

        public bool Test(TimeSpan value) {
            return Test(value.TotalSeconds);
        }

        public bool Test(DateTime value) {
            return Test(value.TimeOfDay);
        }
    }
}