using System;
using System.Globalization;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public class DistanceTestEntry : ITestEntry {
        public static ITestEntryRegister RegisterInstance = new Register();

        private class Register : ITestEntryRegister {
            public ITestEntry Create(Operator op, string value) {
                return ToMeters(value, out var meters) ? new DistanceTestEntry(op, meters) : null;
            }

            public bool TestValue(string value) {
                Logging.Debug(value);
                return value.IndexOf("km", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                        value.IndexOf("cm", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                        value.IndexOf("mm", StringComparison.InvariantCultureIgnoreCase) != -1;
            }

            public bool TestCommonKey(string key) {
                Logging.Debug(key);
                return string.Equals(key, "len", StringComparison.Ordinal) ||
                        string.Equals(key, "length", StringComparison.Ordinal) ||
                        string.Equals(key, "distance", StringComparison.Ordinal);
            }
        }

        private static bool ToMeters([CanBeNull] string value, out double meters) {
            if (value == null) {
                meters = 0d;
                return false;
            }

            if (!FlexibleParser.TryParseDouble(value, out meters)) {
                return false;
            }

            if (value.IndexOf("km", StringComparison.InvariantCultureIgnoreCase) != -1) {
                meters *= 1e3;
            }

            if (value.IndexOf("cm", StringComparison.InvariantCultureIgnoreCase) != -1) {
                meters /= 1e2;
            }

            if (value.IndexOf("mm", StringComparison.InvariantCultureIgnoreCase) != -1) {
                meters /= 1e3;
            }

            return true;
        }

        private readonly Operator _op;
        private readonly double _metersValue;

        public override string ToString() {
            return _op.OperatorToString() + _metersValue.ToString(CultureInfo.InvariantCulture);
        }

        private DistanceTestEntry(Operator op, double metersValue) {
            _op = op;
            _metersValue = metersValue;
        }

        public bool Test(string value) {
            return ToMeters(value, out var val) && Test(val);
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