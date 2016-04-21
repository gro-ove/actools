using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    /// <summary>
    /// Represents a single token.
    /// </summary>
    internal class Token {
        /// <summary>
        /// Represents the token that marks the end of the input.
        /// </summary>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]     // token is immutable
        public static readonly Token End = new Token(string.Empty, Lexer.TokenEnd);

        private readonly string _value;
        private readonly int _tokenType;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Token"/> class.
        /// </summary>
        /// <param name="value">The _value.</param>
        /// <param name="tokenType">Type of the token.</param>
        public Token(string value, int tokenType) {
            _value = value;
            _tokenType = tokenType;
        }

        /// <summary>
        /// Gets the _value.
        /// </summary>
        /// <_value>The _value.</_value>
        public string Value => _value;

        /// <summary>
        /// Gets the type of the token.
        /// </summary>
        /// <_value>The type.</_value>
        public int TokenType => _tokenType;

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString() {
            return string.Format(CultureInfo.InvariantCulture, "{0}: {1}", _tokenType, _value);
        }
    }
}
