using System;
using System.Collections.Generic;
using System.Linq;

namespace AcManager.Tools.Helpers {
    /// <summary>
    /// VDF section.
    /// </summary>
    public class Vdf {
        /// <summary>
        /// String values.
        /// </summary>
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Child sections.
        /// </summary>
        public Dictionary<string, Vdf> Children { get; } = new Dictionary<string, Vdf>();

        /// <summary>
        /// Parse VDF-formatted string.
        /// </summary>
        /// <param name="content">VDF-formatted string.</param>
        /// <exception cref="Exception">Thrown if file is invalid.</exception>
        /// <returns>Root VDF entry.</returns>
        public static Vdf Parse(string content) {
            return Get(new VdfTokenizer(content));
        }

        /// <summary>
        /// Serialize data into VDF format.
        /// </summary>
        /// <returns>Serialized data.</returns>
        public override string ToString() {
            return ToString("");
        }

        #region Serializing
        private string ToString(string padding) {
            return string.Join("", Children.Select(x => $"{padding}\"{x.Key}\"\n{padding}{{\n{x.Value.ToString(padding + '\t')}{padding}}}\n")
                                           .Union(Values.Select(x => $"{padding}\"{x.Key}\"\t\t\"{x.Value}\"\n")));
        }
        #endregion

        #region Parsing
        private enum VdfToken {
            Begin,
            End,
            String
        }

        private static Vdf Get(VdfTokenizer tokenizer, bool child = false) {
            var section = new Vdf();
            while (!tokenizer.IsFinished) {
                switch (tokenizer.ReadNext()) {
                    case null:
                        if (child) throw new Exception("Unexpected end of file");
                        return section;
                    case VdfToken.End:
                        if (!child) throw new Exception("Unexpected end of section");
                        return section;
                    case VdfToken.Begin:
                        throw new Exception("Unexpected begin of section");
                }

                var key = tokenizer.Consume();
                switch (tokenizer.ReadNext()) {
                    case null:
                        throw new Exception("Unexpected end of file");
                    case VdfToken.End:
                        throw new Exception("Unexpected end of section");
                    case VdfToken.Begin:
                        section.Children[key] = Get(tokenizer, true);
                        break;
                    case VdfToken.String:
                        section.Values[key] = tokenizer.Consume();
                        break;
                }
            }

            return section;
        }

        private class VdfTokenizer {
            private readonly string _content;
            private int _pos;
            private string _value;

            public VdfTokenizer(string content) {
                _content = content;
                _pos = 0;
            }

            public bool IsFinished => _pos >= _content.Length;

            public VdfToken? ReadNext() {
                if (IsFinished) return null;

                while (char.IsWhiteSpace(_content[_pos])) {
                    ++_pos;
                    if (IsFinished) return null;
                }

                switch (_content[_pos++]) {
                    case '{':
                        return VdfToken.Begin;
                    case '}':
                        return VdfToken.End;
                    case '"':
                        if (IsFinished) return null;
                        var start = _pos;
                        while (_content[_pos] != '"') {
                            ++_pos;
                            if (IsFinished) return null;
                        }

                        _value = _content.Substring(start, _pos++ - start);
                        return VdfToken.String;
                    default:
                        throw new Exception("Unexpected token");
                }
            }

            public string Consume() {
                var v = _value;
                _value = null;
                return v;
            }
        }
        #endregion
    }
}