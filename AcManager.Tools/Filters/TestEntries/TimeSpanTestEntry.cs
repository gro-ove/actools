using System;
using System.ServiceModel.Configuration;
using JetBrains.Annotations;
using AcTools.Utils.Helpers;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public class TimeSpanTestEntry : ITestEntry, ITestEntryFactory {
        private readonly string _defaultPostfix;

        // For factory
        public TimeSpanTestEntry(string defaultPostfix) {
            _defaultPostfix = defaultPostfix;
        }

        ITestEntry ITestEntryFactory.Create(Operator op, string value) {
            return ToTimeSpan(value, _defaultPostfix, out var timeSpan, out var strict)
                    ? new TimeSpanTestEntry(op, timeSpan, strict, _defaultPostfix) : null;
        }

        private static TimeSpan? GetTimeSpanPostfix(double value, [CanBeNull] string postfix) {
            switch (postfix) {
                case "y":
                case "ye":
                case "yr":
                    return TimeSpan.FromDays(value * 365);
                case "mo":
                case "mn":
                    return TimeSpan.FromDays(value * 30);
                case "w":
                case "we":
                case "wk":
                    return TimeSpan.FromDays(value * 7);
                case "d":
                case "da":
                    return TimeSpan.FromDays(value);
                case "h":
                case "hr":
                case "ho":
                    return TimeSpan.FromHours(value);
                case "m":
                case "mi":
                    return TimeSpan.FromMinutes(value);
                case "s":
                case "sc":
                case "se":
                    return TimeSpan.FromSeconds(value);
                case "ms":
                    return TimeSpan.FromMilliseconds(value);
                default:
                    return null;
            }
        }

        private static bool ToTimeSpan([CanBeNull] string value, [CanBeNull] string defaultPostfix, out TimeSpan timeSpan, out bool strict) {
            if (value == null) {
                timeSpan = default(TimeSpan);
                strict = false;
                return false;
            }

            var postfix = TestEntryFactory.GetPostfix(value, 2)
                    ?? (value.IndexOf(':') == -1 ? defaultPostfix?.Substring(0, Math.Min(defaultPostfix.Length, 2)) : null);
            var fromPostfix = GetTimeSpanPostfix(FlexibleParser.TryParseDouble(value) ?? 1d, postfix);
            if (fromPostfix.HasValue) {
                strict = false;
                timeSpan = fromPostfix.Value;
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

        // For test entry
        private TimeSpanTestEntry(Operator op, TimeSpan value, bool exact, string defaultPostfix) {
            _op = op;
            _value = value;
            _exact = exact;
            _defaultPostfix = defaultPostfix;
        }

        public override string ToString() {
            return _op.OperatorToString() + _value;
        }

        public void Set(ITestEntryFactory factory) {}

        public bool Test(string value) {
            return value != null && (ToTimeSpan(value, _defaultPostfix, out var parsed, out var _) ? Test(parsed)
                    : FlexibleParser.TryParseInt(value, out var val) && Test(val));
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