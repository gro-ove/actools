using System;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class TimeSpanTestEntry : ITestEntry {
        private readonly Operator _op;
        private readonly TimeSpan _value;
        private readonly bool _exact;

        public override string ToString() {
            return (_op == Operator.Less ? "<" : _op == Operator.Equal ? "=" : ">") + _value;
        }

        internal TimeSpanTestEntry(Operator op, TimeSpan value, bool exact = true) {
            _op = op;
            _value = value;
            _exact = exact;
        }

        public bool Test(string value) {
            if (value == null) return false;
            int val;
            return FlexibleParser.TryParseInt(value, out val) && Test(val);
        }

        public bool Test(double value) {
            var i = _exact ? value : Math.Round(value);
            switch (_op) {
                case Operator.Less:
                    return i < _value.TotalSeconds;

                case Operator.LessEqual:
                    return i <= _value.TotalSeconds;

                case Operator.More:
                    return i > _value.TotalSeconds;

                case Operator.MoreEqual:
                    return i >= _value.TotalSeconds;

                case Operator.Equal:
                    return Math.Abs(i - _value.TotalSeconds) < 0.001;

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