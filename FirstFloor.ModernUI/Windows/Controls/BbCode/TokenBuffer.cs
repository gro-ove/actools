using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    /// <summary>
    /// Represents a token buffer.
    /// </summary>
    internal class TokenBuffer {
        private readonly List<Token> _tokens = new List<Token>();
        private int _position;
        //private int mark;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:TokenBuffer"/> class.
        /// </summary>
        /// <param name="lexer">The lexer.</param>
        public TokenBuffer([NotNull] Lexer lexer) {
            if (lexer == null) throw new ArgumentNullException(nameof(lexer));

            Token token;
            do {
                token = lexer.NextToken();
                _tokens.Add(token);
            } while (token.TokenType != Lexer.TokenEnd);
        }

        /// <summary>
        /// Performs a look-ahead.
        /// </summary>
        /// <param name="count">The number of _tokens to look ahead.</param>
        /// <returns></returns>
        public Token La(int count) {
            var index = _position + count - 1;
            return index < _tokens.Count ? _tokens[index] : Token.End;
        }

        ///// <summary>
        ///// Marks the current _position.
        ///// </summary>
        //public void Mark()
        //{
        //    this.mark = this._position;
        //}

        ///// <summary>
        ///// Gets the mark.
        ///// </summary>
        ///// <returns></returns>
        //public Token[] GetMark()
        //{
        //    if (this.mark < this._position) {
        //        Token[] result = new Token[this._position - this.mark];
        //        for (int i = this.mark; i < this._position; i++) {
        //            result[i - this.mark] = this._tokens[i];
        //        }

        //        return result;
        //    }
        //    return new Token[0];
        //}

        /// <summary>
        /// Consumes the next token.
        /// </summary>
        public void Consume() {
            _position++;
        }
    }
}
