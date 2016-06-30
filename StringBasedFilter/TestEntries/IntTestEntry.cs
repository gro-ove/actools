using System;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    internal class IntTestEntry : ITestEntry {
        private readonly Operator _op;
        private readonly int _value;

        public override string ToString() {
            return (_op == Operator.Less ? "<" : _op == Operator.Equal ? "=" : ">") + _value;
        }

        internal IntTestEntry(Operator op, int value) {
            _op = op;
            _value = value;
        }

        public bool Test(string value) {
            if (value == null) return false;
            int val;
            return FlexibleParser.TryParseInt(value, out val) && Test(val);
        }

        public bool Test(double value) {
            var i = (int)Math.Round(value);
            switch (_op) {
                case Operator.Less:
                    return i < _value;

                case Operator.More:
                    return i > _value;

                case Operator.Equal:
                    return i == _value;

                default:
                    return false;
            }
        }

        public bool Test(bool value) {
            return Test(value ? 1.0 : 0.0);
        }
    }
}