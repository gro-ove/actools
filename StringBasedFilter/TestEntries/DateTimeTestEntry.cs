using System;
using System.Globalization;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class DateTimeTestEntry : ITestEntry {
        private readonly Operator _op;
        private readonly DateTime _value;
        private readonly bool _exact;

        public override string ToString() {
            return ((char)_op).ToString(CultureInfo.InvariantCulture) + _value;
        }

        internal DateTimeTestEntry(Operator op, DateTime value, bool exact = true) {
            _op = op;
            _value = value;
            _exact = exact;
        }

        public bool Test(string value) {
            if (value == null) return false;
            int val;
            return FlexibleParser.TryParseInt(value, out val) && Test(val);
        }

        // days ago
        public bool Test(double value) {
            return Test(DateTime.Now - TimeSpan.FromDays(value));
        }

        public bool Test(bool value) {
            return Test(value ? 1.0 : 0.0);
        }

        public bool Test(TimeSpan value) {
            return Test(DateTime.Now.Date + value);
        }

        public bool Test(DateTime value) {
            var delta = _exact ? (long)Math.Floor((value - _value).TotalMinutes) : CompareByDays(value, _value);
            switch (_op) {
                case Operator.Less:
                    return delta < 0;

                case Operator.More:
                    return delta > 0;

                case Operator.Equal:
                    return delta == 0;

                case Operator.LessEqual:
                    return delta <= 0;

                case Operator.MoreEqual:
                    return delta >= 0;

                default:
                    return false;
            }
        }

        private static int CompareByDays(DateTime a, DateTime b) {
            var d = a.Year - b.Year;
            if (d != 0) return d;

            d = a.Month - b.Month;
            if (d != 0) return d;

            d = a.Day - b.Day;
            return d;
        }
    }
}