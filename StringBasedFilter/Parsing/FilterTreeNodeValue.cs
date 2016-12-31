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

        private static readonly Regex ParsingRegex = new Regex(@"^([a-zA-Z]+)(\.[a-zA-Z]+)?\s*([:<>=+-])\s*", RegexOptions.Compiled);

        public static FilterTreeNode Create(string value, bool strictMode, out string keyName) {
            ITestEntry testEntry;

            if (value.Length > 0 && value[0] == '#') {
                keyName = "tag";
                testEntry = CreateTestEntry(value.Substring(1), true, false);
            } else {
                var match = ParsingRegex.Match(value);
                if (match.Success) {
                    keyName = match.Groups[1].Value.ToLower();
                    var op = match.Groups[3].Value[0];
                    var end = value.Substring(match.Length).TrimStart();

                    if (match.Groups[2].Success) {
                        var parser = new FilterParser();
                        string[] properties;
                        return new FilterTreeNodeChild(keyName, parser.Parse($"{match.Groups[2].Value.Substring(1)}{op}{end}", out properties), strictMode);
                    }

                    switch (op) {
                        case ':':
                            testEntry = CreateTestEntry(end, true, false);
                            break;
                        case '+':
                            testEntry = new BooleanTestEntry(true);
                            break;
                        case '-':
                            testEntry = new BooleanTestEntry(false);
                            break;
                        case '<':
                            testEntry = CreateNumericTestEntry(Operator.Less, keyName, end);
                            break;
                        case '>':
                            testEntry = CreateNumericTestEntry(Operator.More, keyName, end);
                            break;
                        case '=':
                            testEntry = CreateNumericTestEntry(Operator.Equal, keyName, end);
                            break;
                        default:
                            testEntry = new ConstTestEntry(false);
                            break;
                    }
                } else {
                    keyName = null;
                    testEntry = CreateTestEntry(value, false, strictMode);
                }
            }

            return new FilterTreeNodeValue(keyName, testEntry);
        }

        private FilterTreeNodeValue(string value, ITestEntry testEntry) {
            Key = value;
            _testEntry = testEntry;
        }

        private static ITestEntry CreateTestEntry(string value, bool wholeMatch, bool strictMode) {
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
                return new RegexTestEntry(RegexFromQuery.Create(value, wholeMatch, strictMode));
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