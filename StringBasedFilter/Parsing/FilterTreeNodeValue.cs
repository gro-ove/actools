using System;
using System.Text.RegularExpressions;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeValue : FilterTreeNode {
        protected static readonly Regex ParsingRegex = new Regex(@"^([a-zA-Z]+)(\.[a-zA-Z]+)?\s*([:<>=+-])\s*", RegexOptions.Compiled);

        private readonly string _key;
        private readonly ITestEntry _testEntry;

        public static FilterTreeNode Create(string value, bool strictMode, out string keyName) {
            ITestEntry testEntry;

            if (value.Length > 0 && value[0] == '#') {
                keyName = "tag";
                testEntry = CreateTestEntry(value.Substring(1), true, false);
            } else {
                var match = ParsingRegex.Match(value);
                if (match.Success) {
                    keyName = match.Groups[1].Value.ToLower();
                    var op = match.Groups[3].Value;
                    var end = value.Substring(match.Length);

                    if (match.Groups[2].Success) {
                        var parser = new FilterParser();
                        string[] properties;
                        return new FilterTreeNodeChild(keyName, parser.Parse($"{match.Groups[2].Value.Substring(1)}{op}{end}", out properties), strictMode);
                    }

                    if (op == ":") {
                        testEntry = CreateTestEntry(end, true, false);
                    } else if (op == "+" || op == "-") {
                        testEntry = new BooleanTestEntry(op == "+");
                    } else if (end.Contains(".") || end.Contains(",")) {
                        double num;
                        testEntry = FlexibleParser.TryParseDouble(end, out num)
                                ? new NumberTestEntry(op == "<" ? Operator.Less : op == "=" ? Operator.Equal : Operator.More, num)
                                : (ITestEntry)new ConstTestEntry(false);
                    } else {
                        int num;
                        testEntry = FlexibleParser.TryParseInt(end, out num)
                                ? new IntTestEntry(op == "<" ? Operator.Less : op == "=" ? Operator.Equal : Operator.More, num)
                                : (ITestEntry)new ConstTestEntry(false);
                    }
                } else {
                    keyName = null;
                    testEntry = CreateTestEntry(value, false, strictMode);
                }
            }

            return new FilterTreeNodeValue(keyName, testEntry);
        }

        private FilterTreeNodeValue(string value, ITestEntry testEntry) {
            _key = value;
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
            return "\"" + (_key == null ? "" : _key + "=") + _testEntry + "\"";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return tester.Test(obj, _key, _testEntry);
        }
    }
}