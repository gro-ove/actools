using System;
using System.Text.RegularExpressions;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeValue : FilterTreeNode {
        internal string Key;
        private readonly ITestEntry _testEntry;

        public static FilterTreeNode Create(string value, FilterParams filterParams, out string keyName) {
            ITestEntry testEntry;

            if (filterParams.ValueConversion != null) {
                value = filterParams.ValueConversion(value);
                if (value == null) {
                    keyName = null;
                    return new FilterTreeNodeEmpty();
                }
            }

            var splitted = filterParams.ValueSplitter.ValueSplitFunc(value);
            if (splitted != null) {
                keyName = splitted.PropertyKey;

                if (splitted.ChildKey != null) {
                    var fakeFilter = $"{splitted.PropertyKey}{(char)splitted.ComparingOperation}{splitted.PropertyValue}";
                    var parser = new FilterParser(filterParams);
                    return new FilterTreeNodeChild(splitted.ChildKey, parser.Parse(fakeFilter, out _), filterParams);
                }

                switch (splitted.ComparingOperation) {
                    case FilterComparingOperation.IsSame:
                        testEntry = CreateTestEntry(splitted.PropertyValue, filterParams.RegexFactory, filterParams.StringMatchMode);
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
                        testEntry = new NumberTestEntry((Operator)splitted.ComparingOperation, splitted.PropertyValue);
                        break;
                    default:
                        testEntry = new ConstTestEntry(false);
                        break;
                }
            } else {
                keyName = null;
                testEntry = CreateTestEntry(value, filterParams.RegexFactory, filterParams.StringMatchMode);
            }

            return new FilterTreeNodeValue(keyName, testEntry);
        }

        private FilterTreeNodeValue(string value, ITestEntry testEntry) {
            Key = value;
            _testEntry = testEntry;
        }

        private static ITestEntry CreateTestEntry(string value, RegexFactory regexFactory, StringMatchMode mode) {
            if (value.Length > 1) {
                if (value[0] == '"' && value[value.Length - 1] == '"') {
                    return new StringTestEntry(value.Substring(1, value.Length - 2), mode);
                }

                if (value[0] == '\'' && value[value.Length - 1] == '\'') {
                    return new StringTestEntry(value.Substring(1, value.Length - 2), StringMatchMode.CompleteMatch);
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

            if (RegexFromQuery.IsQuery(value)) {
                return new RegexTestEntry(regexFactory(value, mode));
            }

            return new StringTestEntry(value, mode);
        }

        public override string ToString() {
            return "\"" + (Key == null ? "" : Key + "=") + _testEntry + "\"";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return tester.Test(obj, Key, _testEntry);
        }
    }
}