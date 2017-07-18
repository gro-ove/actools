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
    public delegate Regex RegexFactory(string query, bool wholeMatch, bool strictMode);

    [CanBeNull]
    public delegate FilterPropertyValue ValueSplitFunc([NotNull] string value);

    [NotNull]
    public delegate ITestEntry BooleanTestFactory(bool value);

    [NotNull]
    public delegate string ValueConversion([NotNull] string rawValue);

    public class FilterParams {
        /// <summary>
        /// Params used by default, changeable.
        /// </summary>
        public static readonly FilterParams Defaults = new FilterParams();

        public bool StrictMode { get; set; }
        public bool FullMatchMode { get; set; }

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
        public ValueSplitFunc ValueSplitFunc { get; set; } = DefaultValueSplitFunc.Default;

        [NotNull]
        public RegexFactory RegexFactory { get; set; } = RegexFromQuery.Create;

        /// <summary>
        /// Default factory for boolean testers, override to make auto-conversion more strict/flexible.
        /// </summary>
        [NotNull]
        public BooleanTestFactory BooleanTestFactory { get; set; } = b => new BooleanTestEntry(b);
    }

    internal static class DefaultValueSplitFunc {
        private static readonly Regex ParsingRegex = new Regex(@"^([a-zA-Z]+)(\.[a-zA-Z]+)?\s*([:<>≥≤=+-])\s*", RegexOptions.Compiled);

        public static FilterPropertyValue Default(string s) {
            var match = ParsingRegex.Match(s);
            if (!match.Success) return null;

            var key = match.Groups[1].Value.ToLower();
            var operation = (FilterComparingOperation)match.Groups[3].Value[0];
            var value = s.Substring(match.Length).TrimStart();

            if (match.Groups[2].Success) {
                var actualKey = match.Groups[2].Value.Substring(1).ToLower();
                return new FilterPropertyValue(actualKey, operation, value) { ChildKey = key };
            }

            return new FilterPropertyValue(key, operation, value);
        }
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

        private bool NextMatchIs(char match) {
            var i = _pos;
            for (; i < _filter.Length; i++) {
                if (char.IsWhiteSpace(_filter[i])) continue;
                if (_filter[i] != match) return false;

                _pos = i + 1;
                return true;
            }

            return false;
        }

        private FilterTreeNode NextNode() {
            return NextNodeOr();
        }

        private FilterTreeNode NextNodeOr() {
            var node = NextNodeNor();

            while (true) {
                if (NextMatchIs('|')) {
                    node = new FilterTreeNodeOr(node, NextNodeNor());
                } else if (NextMatchIs(',')) {
                    var a = node;
                    var b = NextNodeNor();

                    var av = a as FilterTreeNodeValue;
                    if (av != null) {
                        var bv = b as FilterTreeNodeValue;
                        if (bv != null && bv.Key == null) {
                            bv.Key = av.Key;
                        }
                    }

                    node = new FilterTreeNodeOr(a, b);
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
            while (NextMatchIs('&')) {
                node = new FilterTreeNodeAnd(node, NextNodeNot());
            }
            return node;
        }

        private FilterTreeNode NextNodeNot() {
            var inverse = false;
            while (NextMatchIs('!')) {
                inverse = !inverse;
            }
            return inverse ? new FilterTreeNodeNot(NextNodeValue()) : NextNodeValue();
        }

        private FilterTreeNode NextNodeValue() {
            string s = null;
            FilterTreeNode node = null;

            var buffer = new StringBuilder(_filter.Length - _pos);
            for (; _pos < _filter.Length; _pos++) {
                var c = _filter[_pos];
                if (c == '\\') {
                    _pos++;
                    if (_pos < _filter.Length) {
                        buffer.Append(_filter[_pos]);
                    }
                    continue;
                }

                if (c == ')' || c == '&' || c == '|' || c == ',' || c == '^' || c == '!') {
                    break;
                }

                if (c == '`' || c == '"') {
                    var i = _pos + 1;
                    var literal = new StringBuilder(_filter.Length - _pos);
                    literal.Append(c);

                    for (; i < _filter.Length; i++) {
                        var n = _filter[i];
                        if (n == '\\') {
                            i++;
                            if (i < _filter.Length) {
                                literal.Append(_filter[i]);
                            }
                            continue;
                        }

                        literal.Append(n);

                        if (n == c) {
                            goto Ok;
                        }
                    }

                    continue;

                    Ok:
                    buffer.Append(literal);
                    _pos = i;
                    continue;
                }

                if (c == '(') {
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
                }

                buffer.Append(c);
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