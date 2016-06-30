using System.Text.RegularExpressions;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeValue : FilterTreeNode {
        protected static readonly Regex ParsingRegex = new Regex(@"^([a-zA-Z]+)\s*([:<>=+-])\s*", RegexOptions.Compiled);

        private readonly string _key;
        private readonly ITestEntry _testEntry;

        public FilterTreeNodeValue(string value, out string keyName) {
            var match = ParsingRegex.Match(value);
            if (match.Success) {
                _key = match.Groups[1].Value.ToLower();
                var op = match.Groups[2].Value;
                var end = value.Substring(match.Length);

                if (op == ":") {
                    _testEntry = CreateTestEntry(end, true);
                } else if (op == "+" || op == "-") {
                    _testEntry = new BooleanTestEntry(op == "+");
                } else if (end.Contains(".") || end.Contains(",")) {
                    double num;
                    _testEntry = FlexibleParser.TryParseDouble(end, out num)
                            ? new NumberTestEntry(op == "<" ? Operator.Less : op == "=" ? Operator.Equal : Operator.More, num)
                            : (ITestEntry)new ConstTestEntry(false);
                } else {
                    int num;
                    _testEntry = FlexibleParser.TryParseInt(end, out num)
                            ? new IntTestEntry(op == "<" ? Operator.Less : op == "=" ? Operator.Equal : Operator.More, num)
                            : (ITestEntry)new ConstTestEntry(false);
                }
            } else {
                _key = null;
                _testEntry = CreateTestEntry(value, false);
            }

            keyName = _key;
        }

        private static ITestEntry CreateTestEntry(string value, bool wholeMatch) {
            if (value.Contains("*") || value.Contains("?")) {
                return new RegexTestEntry(RegexFromQuery.Create(value, wholeMatch));
            } else {
                return new StringTestEntry(value, wholeMatch);
            }
        }

        public override string ToString() {
            return "\"" + (_key == null ? "" : _key + "=") + _testEntry + "\"";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return tester.Test(obj, _key, _testEntry);
        }
    }
}