using System;
using System.Globalization;
using JetBrains.Annotations;
using StringBasedFilter.Utils;

namespace StringBasedFilter.TestEntries {
    public class NumberTestEntry : ITestEntry {
        private readonly Operator _op;
        private readonly string _originalValue;
        private readonly double _value;

        public override string ToString() {
            return _op.OperatorToString() + _value.ToString(CultureInfo.InvariantCulture);
        }

        internal NumberTestEntry(Operator op, [NotNull] string originalValue) {
            _op = op;
            _originalValue = originalValue;
            _value = FlexibleParser.TryParseDouble(originalValue, out var num) ? num : double.NaN;
        }

        private ITestEntryFactory _overrideFactoryType;
        private ITestEntry _override;

        public void Set(ITestEntryFactory factory){
            if (ReferenceEquals(_overrideFactoryType, factory)) return;
            _overrideFactoryType = factory;
            _override = factory?.Create(_op, _originalValue);
        }

        public bool Test(string value) {
            if (_override != null) return _override.Test(value);
            if (value == null) return false;
            return FlexibleParser.TryParseDouble(value, out var val) && Test(val);
        }

        public bool Test(double value) {
            if (_override != null) return _override.Test(value);

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
            return _override?.Test(value) ?? Test(value ? 1.0 : 0.0);
        }

        public bool Test(TimeSpan value) {
            return _override?.Test(value) ?? Test(value.TotalSeconds);
        }

        public bool Test(DateTime value) {
            return _override?.Test(value) ?? Test(value.TimeOfDay);
        }
    }
}
