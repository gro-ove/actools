using System;
using System.Globalization;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public class FileSizeTestEntry : ITestEntry {
        public static ITestEntryRegister RegisterInstance = new Register();

        private class Register : ITestEntryRegister {
            public ITestEntry Create(Operator op, string value) {
                return ToBytes(value, out var meters) ? new FileSizeTestEntry(op, meters) : null;
            }

            public bool TestValue(string value) {
                return LocalizationHelper.TryParseReadableSize(value, null, out _);
                /*return value.IndexOf("kb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                        value.IndexOf("mb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                        value.IndexOf("gb", StringComparison.InvariantCultureIgnoreCase) != -1 ||
                        value.IndexOf("tb", StringComparison.InvariantCultureIgnoreCase) != -1;*/
            }

            public bool TestCommonKey(string key) {
                return string.Equals(key, "size", StringComparison.Ordinal);
            }
        }

        private static bool ToBytes([CanBeNull] string value, out long bytes) {
            return LocalizationHelper.TryParseReadableSize(value, null, out bytes);

            /*if (value == null) {
                bytes = 0L;
                return false;
            }

            if (!FlexibleParser.TryParseDouble(value, out var parsed)) {
                bytes = 0L;
                return false;
            }

            if (value.IndexOf("kb", StringComparison.InvariantCultureIgnoreCase) != -1) {
                parsed *= 1e3;
            } else if (value.IndexOf("mb", StringComparison.InvariantCultureIgnoreCase) != -1) {
                parsed *= 1e6;
            } else if (value.IndexOf("gb", StringComparison.InvariantCultureIgnoreCase) != -1) {
                parsed *= 1e9;
            } else if (value.IndexOf("tb", StringComparison.InvariantCultureIgnoreCase) != -1) {
                parsed *= 1e9;
            }

            bytes = (long)parsed;
            return true;*/
        }

        private readonly Operator _op;
        private readonly double _metersValue;

        public override string ToString() {
            return _op.OperatorToString() + _metersValue.ToString(CultureInfo.InvariantCulture);
        }

        private FileSizeTestEntry(Operator op, long metersValue) {
            _op = op;
            _metersValue = metersValue;
        }

        public bool Test(string value) {
            return ToBytes(value, out var val) && Test(val);
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