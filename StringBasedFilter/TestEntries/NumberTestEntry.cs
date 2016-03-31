using System;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class NumberTestEntry : ITestEntry {
        internal enum Operator {
            Less, More, Equal
        }

        private readonly Operator _op;
        private readonly double _value;

        public override string ToString() {
            return (_op == Operator.Less ? "<" : _op == Operator.Equal ? "=" : ">") + _value;
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

                case Operator.More:
                    return value > _value;

                case Operator.Equal:
                    return Math.Abs(value - _value) < 0.0001;

                default:
                    return false;
            }
        }

        public bool Test(bool value) {
            return Test(value ? 1.0 : 0.0);
        }
    }
}
