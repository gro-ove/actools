using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StringBasedFilter.Parsing {
    internal class FilterParser {
        private int _pos;
        private string _filter;
        private List<string> _properties;

        internal FilterTreeNode Parse(string filter, out string[] properies) {
            _pos = 0;
            _filter = filter;
            _properties = new List<string>();
            var result = NextNode();
            properies = _properties.Distinct().ToArray();
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
            while (NextMatchIs('|') || NextMatchIs(',')) {
                node = new FilterTreeNodeOr(node, NextNodeNor());
            }
            return node;
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

            var buffer = new StringBuilder();
            for (; _pos < _filter.Length; _pos++) {
                var c = _filter[_pos];
                if (c == '\\') continue;
                if (c == ')' || c == '&' || c == '|' || c == '^' || c == '!') break;

                if (c == '(') {
                    var value = buffer.ToString().Trim();

                    if (value.Length > 0) {
                        var oldProperties = _properties;
                        _properties = new List<string>();

                        _pos++;
                        node = NextNode();
                        _pos++;

                        node = new FilterTreeNodeChild(value, node);
                        _properties = oldProperties;
                        s = value;
                    } else {
                        _pos++;
                        node = NextNode();
                        _pos++;
                    }

                    break;
                }

                buffer.Append(_filter[_pos]);
            }

            if (node == null) {
                node = new FilterTreeNodeValue(buffer.ToString().Trim(), out s);
            }

            if (s != null) {
                _properties.Add(s);
            }

            return node;
        }
    }
}