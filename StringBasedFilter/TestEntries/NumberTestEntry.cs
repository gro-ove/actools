using System;
using System.Globalization;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class NumberTestEntry : ITestEntry {
        private readonly Operator _op;
        private readonly double _value;

        public override string ToString() {
            return (_op == Operator.Less ? "<" : _op == Operator.Equal ? "=" : ">") + _value.ToString(CultureInfo.InvariantCulture);
        }

        internal NumberTestEntry(Operator op, double value) {
            _op = op;
            _value = value;
        }

        public bool Test(string value) {
            if (value == null) return false;
            double val;
            return FlexibleParser.TryParseDouble(value, out val) && Test(val);
        }

        public bool Test(double value) {
            switch (_op) {
                case Operator.Less:
                    return value < _value;

                case Operator.LessEqual:
                    return value <= _value;

                case Operator.More:
                    return value > _value;

                case Operator.MoreEqual:
                    return value >= _value;

                case Operator.Equal:
                    return Math.Abs(value - _value) < 0.0001;

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
