using System;
using JetBrains.Annotations;
using AcTools.Utils.Helpers;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public class TimeSpanTestEntry : ITestEntry {
        public static ITestEntryRegister RegisterInstance = new Register();

        private class Register : ITestEntryRegister {
            public ITestEntry Create(Operator op, string value) {
                return ToTimeSpan(value, out var timeSpan, out var strict) ? new TimeSpanTestEntry(op, timeSpan, strict) : null;
            }

            public bool TestValue(string value) {
                return ToTimeSpan(value, out _, out _);
            }

            public bool TestCommonKey(string key) {
                return string.Equals(key, "time", StringComparison.Ordinal) ||
                        string.Equals(key, "duration", StringComparison.Ordinal);
            }
        }

        private static bool ToTimeSpan([CanBeNull] string value, out TimeSpan timeSpan, out bool strict) {
            if (value == null) {
                timeSpan = default(TimeSpan);
                strict = false;
                return false;
            }

            if (value.IndexOf("year", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromDays((FlexibleParser.TryParseDouble(value) ?? 1d) * 365);
                return true;
            }

            if (value.IndexOf("month", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromDays((FlexibleParser.TryParseDouble(value) ?? 1d) * 30);
                return true;
            }

            if (value.IndexOf("week", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromDays((FlexibleParser.TryParseDouble(value) ?? 1d) * 7);
                return true;
            }

            if (value.IndexOf("day", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromDays(FlexibleParser.TryParseDouble(value) ?? 1d);
                return true;
            }

            if (value.IndexOf("hour", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromHours(FlexibleParser.TryParseDouble(value) ?? 1d);
                return true;
            }

            if (value.IndexOf("minute", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromMinutes(FlexibleParser.TryParseDouble(value) ?? 1d);
                return true;
            }

            if (value.IndexOf("second", StringComparison.InvariantCultureIgnoreCase) != -1) {
                strict = false;
                timeSpan = TimeSpan.FromSeconds(FlexibleParser.TryParseDouble(value) ?? 1d);
                return true;
            }

            var p = value.Split(':');
            double? result;
            switch (p.Length) {
                case 2:
                    result = FlexibleParser.TryParseDouble(p[0]) * 60 + FlexibleParser.TryParseDouble(p[1]);
                    break;
                case 3:
                    result = (FlexibleParser.TryParseDouble(p[0]) * 60 + FlexibleParser.TryParseDouble(p[1])) * 60 +
                            FlexibleParser.TryParseDouble(p[2]);
                    break;
                case 4:
                    result = ((FlexibleParser.TryParseDouble(p[0]) * 24 + FlexibleParser.TryParseDouble(p[1])) * 60 +
                            FlexibleParser.TryParseDouble(p[2])) * 60 + FlexibleParser.TryParseDouble(p[3]);
                    break;
                default:
                    timeSpan = default(TimeSpan);
                    strict = false;
                    return false;
            }

            if (!result.HasValue) {
                timeSpan = default(TimeSpan);
                strict = false;
                return false;
            }

            timeSpan = TimeSpan.FromSeconds(result.Value);
            strict = value.IndexOf('.') != -1 || value.IndexOf(',') != -1;
            return true;
        }

        private readonly Operator _op;
        private readonly TimeSpan _value;
        private readonly bool _exact;

        public override string ToString() {
            return _op.OperatorToString() + _value;
        }

        private TimeSpanTestEntry(Operator op, TimeSpan value, bool exact) {
            _op = op;
            _value = value;
            _exact = exact;
        }

        public bool Test(string value) {
            if (value == null) return false;
            return FlexibleParser.TryParseInt(value, out var val) && Test(val);
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
                    return Math.Abs(i - _value.TotalSeconds) < 1;

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