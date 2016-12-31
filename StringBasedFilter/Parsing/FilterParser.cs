using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

namespace StringBasedFilter.Parsing {
    internal class FilterParser {
        internal bool StrictMode;

        private int _pos;
        private string _filter;

        [ItemCanBeNull]
        private List<string> _properties;

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

                        node = new FilterTreeNodeChild(value, node, StrictMode);
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
                node = FilterTreeNodeValue.Create(buffer.ToString().Trim(), StrictMode, out s);
            }

            if (!_properties.Contains(s)) {
                _properties.Add(s);
            }

            return node;
        }
    }
}