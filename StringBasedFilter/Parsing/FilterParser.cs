using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace StringBasedFilter.Parsing {
    public enum FilterComparingOperation {
        IsSame = ':',
        IsTrue = '+',
        IsFalse = '-',
        MoreThan = '>',
        LessThan = '<',
        MoreThanOrEqualTo = '≥',
        LessThanOrEqualTo = '≤',
        EqualTo = '=',
    }

    public static class FilterComparingOperations {
        public static FilterComparingOperation Parse([CanBeNull] string key) {
            if (key?.Length == 1 && Enum.IsDefined(typeof(FilterComparingOperation), (int)key[0])) {
                return (FilterComparingOperation)key[0];
            }

            switch (key) {
                case "−":
                    return FilterComparingOperation.IsFalse;
                case ">=":
                case "=>":
                    return FilterComparingOperation.MoreThanOrEqualTo;
                case "<=":
                case "=<":
                    return FilterComparingOperation.LessThanOrEqualTo;
                default:
                    return FilterComparingOperation.IsSame;
            }
        }
    }

    public class FilterPropertyValue {
        [CanBeNull]
        public string ChildKey { get; set; }

        [NotNull]
        public string PropertyKey { get; }

        public FilterComparingOperation ComparingOperation { get; }

        [NotNull]
        public string PropertyValue { get; }

        public FilterPropertyValue([NotNull] string propertyKey, FilterComparingOperation comparingOperation, [NotNull] string propertyValue) {
            PropertyKey = propertyKey;
            ComparingOperation = comparingOperation;
            PropertyValue = propertyValue;
        }

        public FilterPropertyValue([NotNull] string propertyKey, FilterComparingOperation comparingOperation) {
            PropertyKey = propertyKey;
            ComparingOperation = comparingOperation;
            PropertyValue = "";
        }
    }

    [NotNull]
    public delegate Regex RegexFactory(string query, StringMatchMode mode);

    [CanBeNull]
    public delegate FilterPropertyValue ValueSplitFunc([NotNull] string value);

    [NotNull]
    public delegate ITestEntry BooleanTestFactory(bool value);

    [CanBeNull]
    public delegate ITestEntry CustomTestEntryFactory(string value);

    [CanBeNull]
    public delegate ITestEntry ExtraTestEntryFactory([CanBeNull] string key);

    [NotNull]
    public delegate string ValueConversion([NotNull] string rawValue);

    public class FilterParams {
        /// <summary>
        /// Params used by default, changeable.
        /// </summary>
        public static readonly FilterParams Defaults = new FilterParams();

        /// <summary>
        /// How to compare strings with queries.
        /// </summary>
        public StringMatchMode StringMatchMode { get; set; } = StringMatchMode.IncludedWithin;

        /// <summary>
        /// Converts input value before splitting if needed. Can be null.
        /// </summary>
        [CanBeNull]
        public ValueConversion ValueConversion { get; set; } =
            s => s.StartsWith("#") ? "tag:" + s.Substring(1) : s;

        /// <summary>
        /// Either splits value to three parts: child key (optional), property key and comparing operation
        /// or returns null for simple mode without key.
        /// </summary>
        [NotNull]
        public ValueSplitter ValueSplitter { get; set; } = ValueSplitter.Default;

        /// <summary>
        /// Defines how to build Regex objects from queries.
        /// </summary>
        [NotNull]
        public RegexFactory RegexFactory { get; set; } = RegexFromQuery.Create;

        /// <summary>
        /// Default factory for boolean testers, override to make auto-conversion more strict/flexible.
        /// </summary>
        [NotNull]
        public BooleanTestFactory BooleanTestFactory { get; set; } = b => new BooleanTestEntry(b);

        /*/// <summary>
        /// Factory for extra TestEntry objects, for testing stuff like distances or weights.
        /// </summary>
        [NotNull]
        public ExtraTestEntryFactory ExtraTestEntryFactory { get; set; } = key => null;*/

        [CanBeNull]
        public CustomTestEntryFactory CustomTestEntryFactory { get; set; }
    }

    internal static class DefaultValueSplitFunc {
        private static readonly Regex ParsingRegex = new Regex(@"^([a-zA-Z]+)(\.[a-zA-Z]+)?\s*((?:>=|<=|=>|=<|[:<>≥≤=])\s*|[+\-−]\s*$)", RegexOptions.Compiled);
        public static readonly char[] Separators = { ':', '<', '>', '≥', '≤', '=', '+', '-', '−' };

        public static FilterPropertyValue Default(string s) {
            var match = ParsingRegex.Match(s);
            if (!match.Success) return null;

            var key = match.Groups[1].Value.ToLower();
            var operation = FilterComparingOperations.Parse(match.Groups[3].Value.TrimEnd());
            var value = s.Substring(match.Length).TrimStart();

            if (match.Groups[2].Success) {
                var actualKey = match.Groups[2].Value.Substring(1).ToLower();
                return new FilterPropertyValue(actualKey, operation, value) { ChildKey = key };
            }

            return new FilterPropertyValue(key, operation, value);
        }
    }

    public class ValueSplitter {
        public ValueSplitter([NotNull] ValueSplitFunc valueSplitFunc, [NotNull] params char[] keywordSplitCharacters) {
            ValueSplitFunc = valueSplitFunc;
            KeywordSplitCharacters = keywordSplitCharacters;
        }

        [NotNull]
        public ValueSplitFunc ValueSplitFunc { get; }

        [NotNull]
        public char[] KeywordSplitCharacters { get; }

        public static ValueSplitter Default { get; } = new ValueSplitter(DefaultValueSplitFunc.Default, DefaultValueSplitFunc.Separators);
    }

    internal class FilterParser {
        [NotNull]
        private readonly FilterParams _params;

        private int _pos;
        private string _filter;

        [ItemCanBeNull]
        private List<string> _properties;

        public FilterParser([CanBeNull] FilterParams filterParams) {
            _params = filterParams ?? FilterParams.Defaults;
        }

        internal FilterTreeNode Parse(string filter, out string[] properies) {
            _pos = 0;
            _filter = filter;
            _properties = new List<string>();
            var result = NextNode();
            properies = _properties.ToArray();
            return result;
        }

        private static bool IsTwoOperandsControlChar(char c) {
            return c == '&' || c == '|' || c == ',' || c == '^';
        }

        private bool IsKeywordSeparatorChar(char c) {
            return Array.IndexOf(_params.ValueSplitter.KeywordSplitCharacters, c) != -1;
        }

        private static bool IsControlChar(char c) {
            return IsTwoOperandsControlChar(c);
        }

        private char GetNextNonSpace(out int pos) {
            pos = _pos;
            for (; pos < _filter.Length; pos++) {
                if (char.IsWhiteSpace(_filter[pos])) continue;
                return _filter[pos];
            }

            return (char)0;
        }

        private bool NextMatchIs(char match) {
            if (GetNextNonSpace(out var pos) != match) return false;
            _pos = pos + 1;
            return true;
        }

        private FilterTreeNode NextNode() {
            return NextNodeOr();
        }

        private FilterTreeNode NextNodeOr() {
            var node = NextNodeNor();
            var previousNode = node;
            var nodeKeySet = false;
            string nodeKey = null;
            var nodeKeyNot = false;

            while (true) {
                if (NextMatchIs('|')) {
                    previousNode = NextNodeNor();
                    nodeKeySet = false;
                    node = new FilterTreeNodeOr(node, previousNode);
                } else if (NextMatchIs(',')) {
                    var b = NextNodeNor();

                    if (!nodeKeySet) {
                        nodeKeyNot = previousNode is FilterTreeNodeNot;
                        nodeKey = ((nodeKeyNot ? ((FilterTreeNodeNot)previousNode).A : previousNode) as FilterTreeNodeValue)?.Key;
                        nodeKeySet = nodeKey != null;
                    }

                    if (nodeKey != null) {
                        if (b is FilterTreeNodeValue bv && bv.Key == null) {
                            bv.Key = nodeKey;
                            if (nodeKeyNot) {
                                b = new FilterTreeNodeNot(b);
                            }
                        } else {
                            previousNode = b;
                            nodeKeySet = false;
                        }
                    }

                    node = new FilterTreeNodeOr(node, b);
                } else {
                    return node;
                }
            }
        }

        private FilterTreeNode NextNodeNor() {
            var node = NextNodeAnd();
            while (NextMatchIs('^')) {
                node = new FilterTreeNodeNor(node, NextNodeAnd());
            }
            return node;
        }

        private FilterTreeNode NextNodeAnd() {
            var node = NextNodeNot();
            var previousNode = node;
            var nodeKeySet = false;
            string nodeKey = null;
            var nodeKeyNot = false;

            while (true) {
                if (NextMatchIs('&')) {
                    previousNode = NextNodeNot();
                    nodeKeySet = false;
                    node = new FilterTreeNodeAnd(node, previousNode);
                } else if (!IsTwoOperandsControlChar(GetNextNonSpace(out _)) && _filter.Length > _pos && _filter[_pos] == ' ') {
                    var b = NextNodeNot();

                    if (!nodeKeySet) {
                        nodeKeyNot = previousNode is FilterTreeNodeNot;
                        nodeKey = ((nodeKeyNot ? ((FilterTreeNodeNot)previousNode).A : previousNode) as FilterTreeNodeValue)?.Key;
                        nodeKeySet = nodeKey != null;
                    }

                    if (nodeKey != null) {
                        if (b is FilterTreeNodeValue bv && bv.Key == null) {
                            bv.Key = nodeKey;
                            if (nodeKeyNot) {
                                b = new FilterTreeNodeNot(b);
                            }
                        } else {
                            previousNode = b;
                            nodeKeySet = false;
                        }
                    }

                    node = new FilterTreeNodeAnd(node, b);
                } else {
                    return node;
                }
            }
        }

        private FilterTreeNode NextNodeNot() {
            var inverse = false;
            while (NextMatchIs('!')) {
                inverse = !inverse;
            }
            return inverse ? new FilterTreeNodeNot(NextNodeValue()) : NextNodeValue();
        }

        private void SkipSpaces() {
            for (; _pos < _filter.Length && _filter[_pos] == ' '; _pos++) { }
        }

        [NotNull]
        private FilterTreeNode NextNodeValue() {
            string s = null;
            FilterTreeNode node = null;
            var buffer = new StringBuilder(_filter.Length - _pos);

            SkipSpaces();

            var previousNonSpace = (char)0;
            var previousNonSpaceIndex = -1;
            var keywordSeparatorIncluded = false;

            for (; _pos < _filter.Length; _pos++) {
                var c = _filter[_pos];
                if (c == '\\') {
                    _pos++;
                    if (_pos < _filter.Length) {
                        var e = _filter[_pos];
                        if (RegexFromQuery.IsQuerySymbol(e)) {
                            buffer.Append('\\');
                        }

                        buffer.Append(e);
                    }
                    continue;
                }

                if (c != ' ') {
                    previousNonSpace = c;
                    previousNonSpaceIndex = _pos;
                } else {
                    var nextNonSpace = GetNextNonSpace(out var nonSpacePos);
                    if (IsControlChar(nextNonSpace)) break;
                    if (IsKeywordSeparatorChar(nextNonSpace) || IsKeywordSeparatorChar(previousNonSpace)) {
                        _pos = nonSpacePos - 1;
                        continue;
                    }

                    // Complex and questionable case: ignore spaces when they separate a number from a word, but only then
                    if (keywordSeparatorIncluded && previousNonSpaceIndex >= 0 && char.IsDigit(previousNonSpace) && char.IsLetter(nextNonSpace)) {
                        for (var j = previousNonSpaceIndex - 1; j >= 0; j--) {
                            var p = _filter[j];
                            if (char.IsLetter(p)) goto BreakPiece;
                            if (!char.IsDigit(p)) break;
                        }

                        for (var j = nonSpacePos + 1; j < _filter.Length; j++) {
                            var p = _filter[j];
                            if (char.IsDigit(p)) goto BreakPiece;
                            if (!char.IsLetter(p)) break;
                        }

                        _pos = nonSpacePos - 1;
                        continue;
                    }

                    BreakPiece:
                    break;
                }

                if (IsControlChar(c) || c == ')') {
                    break;
                }

                if (IsKeywordSeparatorChar(c)) {
                    keywordSeparatorIncluded = true;
                }

                if (c == '`' || c == '"' || c == '\'') {
                    var i = _pos + 1;
                    var literal = new StringBuilder(_filter.Length - _pos);
                    literal.Append(c);

                    for (; i < _filter.Length; i++) {
                        var n = _filter[i];
                        if (n == '\\' && (c != '`' || i + 1 < _filter.Length && _filter[i + 1] == '`')) {
                            i++;
                            if (i < _filter.Length) {
                                literal.Append(_filter[i]);
                            }
                            continue;
                        }

                        literal.Append(n);

                        if (n == c) {
                            // If there is only one quote symbol, it’s not going to get processed as quoted text
                            buffer.Append(literal);
                            _pos = i;
                            break;
                        }
                    }
                } else if (c == '(') {
                    var value = buffer.ToString().Trim();

                    if (value.Length > 0) {
                        var oldProperties = _properties;
                        _properties = new List<string>();

                        _pos++;
                        node = NextNode();
                        _pos++;

                        node = new FilterTreeNodeChild(value, node, _params);
                        _properties = oldProperties;
                        s = value;
                    } else {
                        _pos++;
                        node = NextNode();
                        _pos++;
                    }

                    break;
                } else {
                    buffer.Append(c);
                }
            }

            if (node == null) {
                node = FilterTreeNodeValue.Create(buffer.ToString().Trim(), _params, out s);
            }

            if (!_properties.Contains(s)) {
                _properties.Add(s);
            }

            return node;
        }
    }
}