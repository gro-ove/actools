using System;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    /// <summary>
    /// The BbCode lexer.
    /// </summary>
    internal class BbCodeLexer : Lexer {
        private const string TagNewline = "br";

        private static readonly char[] QuoteChars = { '\'', '"' };
        private static readonly char[] WhitespaceChars = { ' ', '\t' };
        private static readonly char[] NewlineChars = { '\r', '\n' };

        /// <summary>
        /// Start tag
        /// </summary>
        public const int TokenStartTag = 0;
        /// <summary>
        /// End tag
        /// </summary>
        public const int TokenEndTag = 1;
        /// <summary>
        /// Attribute
        /// </summary>
        public const int TokenAttribute = 2;
        /// <summary>
        /// Text
        /// </summary>
        public const int TokenText = 3;
        /// <summary>
        /// Line break
        /// </summary>
        public const int TokenLineBreak = 4;

        /// <summary>
        /// Normal state
        /// </summary>
        public const int StateNormal = 0;
        /// <summary>
        /// Tag state
        /// </summary>
        public const int StateTag = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:BBCodeLexer"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        public BbCodeLexer(string value) : base(value) {}

        private bool IsTagNameChar() {
            return IsInRange('A', 'Z') || IsInRange('a', 'z') || IsInRange(new[] { '*' });
        }

        private Token OpenTag() {
            Match('[');
            Mark();
            while (IsTagNameChar()) {
                Consume();
            }

            return new Token(GetMark(), TokenStartTag);
        }

        private Token CloseTag() {
            Match('[');
            Match('/');

            Mark();
            while (IsTagNameChar()) {
                Consume();
            }
            var token = new Token(GetMark(), TokenEndTag);
            Match(']');

            return token;
        }

        private Token Newline() {
            Match('\r', 0, 1);
            Match('\n');

            return new Token(string.Empty, TokenLineBreak);
        }

        private Token Text(bool force) {
            Mark();
            for (var c = La(1); force || c != '[' && c != char.MaxValue && !IsInRange(NewlineChars); c = La(1)) {
                if (force) {
                    force = false;
                }

                if (c == '\\') {
                    Consume();
                }

                Consume();
            }

            return new Token(GetMark().Replace("\\[", "["), TokenText);
        }

        private Token Attribute() {
            Match('=');
            while (IsInRange(WhitespaceChars)) {
                Consume();
            }

            Token token;

            if (IsInRange(QuoteChars)) {
                Consume();
                Mark();
                while (!IsInRange(QuoteChars)) {
                    if (La(1) == '\\') {
                        Consume();
                    }

                    Consume();
                }
                token = new Token(GetMark().Replace("\\\"", "\"").Replace("\\'", "'"), TokenAttribute);
                Consume();
            } else {
                Mark();
                while (!IsInRange(WhitespaceChars) && La(1) != ']' && La(1) != char.MaxValue) {
                    Consume();
                }

                token = new Token(GetMark(), TokenAttribute);
            }

            while (IsInRange(WhitespaceChars)) {
                Consume();
            }
            return token;
        }

        /// <summary>
        /// Gets the default state of the lexer.
        /// </summary>
        /// <value>The state of the default.</value>
        protected override int DefaultState => StateNormal;

        /// <summary>
        /// Gets the next token.
        /// </summary>
        /// <returns></returns>
        public override Token NextToken() {
            while (true) {
                if (La(1) == char.MaxValue) {
                    return Token.End;
                }

                switch (State) {
                    case StateNormal:
                        if (La(1) == '[') {
                            Keep();
                            try {
                                if (La(2) == '/') {
                                    return CloseTag();
                                }

                                var token = OpenTag();
                                if (token.Value == string.Empty || La(1) != '=' && La(1) != ']') {
                                    Restore();
                                    return Text(true);
                                }

                                if (string.Equals(token.Value, TagNewline, StringComparison.OrdinalIgnoreCase)) {
                                    Match(']');
                                    return new Token(string.Empty, TokenLineBreak);
                                }

                                PushState(StateTag);
                                return token;
                            } catch (ParseException) {
                                Restore();
                                return Text(true);
                            }
                        }

                        return IsInRange(NewlineChars) ? Newline() : Text(false);

                    case StateTag:
                        if (La(1) == '=') {
                            Keep();
                            try {
                                return Attribute();
                            } catch (ParseException) {
                                PopState();
                                Restore();
                                return Text(true);
                            }
                        }

                        PopState();
                        if (La(1) == ']') {
                            Consume();
                            break;
                        }

                        return Text(true);

                    default:
                        throw new ParseException("Invalid state");
                }
            }
        }
    }
}
