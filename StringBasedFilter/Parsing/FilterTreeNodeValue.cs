using System;
using System.Text.RegularExpressions;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeValue : FilterTreeNode {
        internal string Key;
        private readonly ITestEntry _testEntry;

        private static ITestEntry CreateTimeSpanTestEntry(Operator op, string value) {
            var p = value.Split(':');
            double? result;
            switch (p.Length) {
                case 0:
                    result = null;
                    break;
                case 1:
                    result = FlexibleParser.TryParseDouble(p[0]);
                    break;
                case 2:
                    result = FlexibleParser.TryParseDouble(p[0]) * 60 + FlexibleParser.TryParseDouble(p[1]);
                    break;
                case 3:
                    result = (FlexibleParser.TryParseDouble(p[0]) * 60 + FlexibleParser.TryParseDouble(p[1])) * 60 +
                            FlexibleParser.TryParseDouble(p[2]);
                    break;
                default:
                    result = ((FlexibleParser.TryParseDouble(p[0]) * 24 + FlexibleParser.TryParseDouble(p[1])) * 60 +
                            FlexibleParser.TryParseDouble(p[2])) * 60 + FlexibleParser.TryParseDouble(p[3]);
                    break;
            }

            if (!result.HasValue) return new ConstTestEntry(false);
            return new TimeSpanTestEntry(op, TimeSpan.FromSeconds(result.Value), value.IndexOf('.') != -1 || value.IndexOf(',') != -1);
        }

        private static ITestEntry CreateDateTimeTestEntry(Operator op, string value) {
            try {
                var date = DateTime.Parse(value);
                return new DateTimeTestEntry(op, date, value.IndexOf(' ') != -1);
            } catch (FormatException) {
                return new ConstTestEntry(false);
            }
        }

        private static ITestEntry CreateNumericTestEntry(Operator op, string key, string value) {
            if (value.IndexOf(':') != -1) {
                return CreateTimeSpanTestEntry(op, value);
            }

            var point = value.IndexOf('.');
            if (point != -1 && value.IndexOf('.', point + 1) != -1 || value.IndexOf('/') != -1 || value.IndexOf('-') > 0) {
                return CreateDateTimeTestEntry(op, value);
            }

            if (DistanceTestEntry.IsDistanceKey(key) || DistanceTestEntry.IsDistanceValue(value)) {
                double num;
                return DistanceTestEntry.ToMeters(value, out num) ? new DistanceTestEntry(op, num) :
                        (ITestEntry)new ConstTestEntry(false);
            }

            if (FileSizeTestEntry.IsDistanceKey(key) || FileSizeTestEntry.IsDistanceValue(value)) {
                double num;
                return FileSizeTestEntry.ToBytes(value, out num) ? new FileSizeTestEntry(op, num) :
                        (ITestEntry)new ConstTestEntry(false);
            }

            if (point != -1 || value.IndexOf(',') != -1) {
                double num;
                return FlexibleParser.TryParseDouble(value, out num) ? new NumberTestEntry(op, num) :
                        (ITestEntry)new ConstTestEntry(false);
            } else {
                int num;
                return FlexibleParser.TryParseInt(value, out num) ? new IntTestEntry(op, num) :
                        (ITestEntry)new ConstTestEntry(false);
            }
        }

        public static FilterTreeNode Create(string value, FilterParams filterParams, out string keyName) {
            ITestEntry testEntry;

            if (filterParams.ValueConversion != null) {
                value = filterParams.ValueConversion(value);
                if (value == null) {
                    keyName = null;
                    return new FilterTreeNodeEmpty();
                }
            }

            var splitted = filterParams.ValueSplitFunc(value);
            if (splitted != null) {
                keyName = splitted.PropertyKey;

                if (splitted.ChildKey != null) {
                    var fakeFilter = $"{splitted.PropertyKey}{splitted.ComparingOperation}{splitted.PropertyValue}";
                    var parser = new FilterParser(filterParams);
                    string[] properties;
                    return new FilterTreeNodeChild(splitted.ChildKey, parser.Parse(fakeFilter, out properties), filterParams);
                }

                switch (splitted.ComparingOperation) {
                    case FilterComparingOperation.IsSame:
                        testEntry = CreateTestEntry(splitted.PropertyValue, filterParams.RegexFactory, true, false);
                        break;
                    case FilterComparingOperation.IsTrue:
                        testEntry = filterParams.BooleanTestFactory(true);
                        break;
                    case FilterComparingOperation.IsFalse:
                        testEntry = filterParams.BooleanTestFactory(false);
                        break;
                    case FilterComparingOperation.LessThan:
                    case FilterComparingOperation.MoreThan:
                    case FilterComparingOperation.LessThanOrEqualTo:
                    case FilterComparingOperation.MoreThanOrEqualTo:
                    case FilterComparingOperation.EqualTo:
                        testEntry = CreateNumericTestEntry((Operator)splitted.ComparingOperation, keyName, splitted.PropertyValue);
                        break;
                    default:
                        testEntry = new ConstTestEntry(false);
                        break;
                }
            } else {
                keyName = null;
                testEntry = CreateTestEntry(value, filterParams.RegexFactory, filterParams.FullMatchMode, filterParams.StrictMode);
            }

            return new FilterTreeNodeValue(keyName, testEntry);
        }

        private FilterTreeNodeValue(string value, ITestEntry testEntry) {
            Key = value;
            _testEntry = testEntry;
        }

        private static ITestEntry CreateTestEntry(string value, RegexFactory regexFactory, bool wholeMatch, bool strictMode) {
            if (value.Length > 1) {
                if (value[0] == '"' && value[value.Length - 1] == '"') {
                    return new StringTestEntry(value.Substring(1, value.Length - 2), wholeMatch, true);
                }

                if (value[0] == '`' && value[value.Length - 1] == '`') {
                    try {
                        var regex = new Regex(value.Substring(1, value.Length - 2), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        return new RegexTestEntry(regex);
                    } catch (Exception) {
                        return new ConstTestEntry(false);
                    }
                }
            }

            if (value.Contains("*") || value.Contains("?")) {
                return new RegexTestEntry(regexFactory(value, wholeMatch, strictMode));
            }

            return new StringTestEntry(value, wholeMatch, strictMode);
        }

        public override string ToString() {
            return "\"" + (Key == null ? "" : Key + "=") + _testEntry + "\"";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return tester.Test(obj, Key, _testEntry);
        }
    }
}