using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    /// <summary>
    /// Provides basic lexer functionality.
    /// </summary>
    internal abstract class Lexer {
        /// <summary>
        /// Defines the end-of-file token type.
        /// </summary>
        public const int TokenEnd = int.MaxValue;

        private readonly CharBuffer _buffer;
        private readonly Stack<int> _states;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Lexer"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        protected Lexer(string value) {
            _buffer = new CharBuffer(value);
            _states = new Stack<int>();
        }

        [AssertionMethod]
        private static void ValidateOccurence(int count, int minOccurs, int maxOccurs) {
            if (count < minOccurs || count > maxOccurs) {
                throw new ParseException("Invalid number of characters");
            }
        }

        /// <summary>
        /// Gets the default state of the lexer.
        /// </summary>
        /// <value>The state of the default.</value>
        protected abstract int DefaultState { get; }

        /// <summary>
        /// Gets the current state of the lexer.
        /// </summary>
        /// <value>The state.</value>
        protected int State => _states.Count > 0 ? _states.Peek() : DefaultState;

        /// <summary>
        /// Pushes a new state on the stac.
        /// </summary>
        /// <param name="state">The state.</param>
        protected void PushState(int state) {
            _states.Push(state);
        }

        /// <summary>
        /// Pops the state.
        /// </summary>
        /// <returns></returns>
        protected int PopState() {
            return _states.Pop();
        }

        /// <summary>
        /// Performs a look-ahead.
        /// </summary>
        /// <param name="count">The number of characters to look ahead.</param>
        /// <returns></returns>
        protected char La(int count) {
            return _buffer.La(count);
        }

        /// <summary>
        /// Marks the current position.
        /// </summary>
        protected void Mark() {
            _buffer.Mark();
        }

        /// <summary>
        /// Gets the mark.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        protected string GetMark() {
            return _buffer.GetMark();
        }

        /// <summary>
        /// Consumes the next character.
        /// </summary>
        protected void Consume() {
            _buffer.Consume();
        }

        /// <summary>
        /// Determines whether the current character is in given range.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="last">The last.</param>
        /// <returns>
        /// 	<c>true</c> if the current character is in given range; otherwise, <c>false</c>.
        /// </returns>
        protected bool IsInRange(char first, char last) {
            var la = La(1);
            return la >= first && la <= last;
        }

        /// <summary>
        /// Determines whether the current character is in given range.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// 	<c>true</c> if the current character is in given range; otherwise, <c>false</c>.
        /// </returns>
        protected bool IsInRange(char[] value) {
            if (value == null) {
                return false;
            }
            var la = La(1);
            return value.Any(t => la == t);
        }

        /// <summary>
        /// Matches the specified character.
        /// </summary>
        /// <param name="value">The value.</param>
        protected void Match(char value) {
            if (La(1) == value) {
                Consume();
            } else {
                throw new ParseException("Character mismatch");
            }
        }

        /// <summary>
        /// Matches the specified character.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="minOccurs">The min occurs.</param>
        /// <param name="maxOccurs">The max occurs.</param>
        protected void Match(char value, int minOccurs, int maxOccurs) {
            var i = 0;
            while (La(1) == value) {
                Consume();
                i++;
            }
            ValidateOccurence(i, minOccurs, maxOccurs);
        }

        /// <summary>
        /// Matches the specified string.
        /// </summary>
        /// <param name="value">The value.</param>
        protected void Match([NotNull] string value) {
            if (value == null) throw new ArgumentNullException(nameof(value));

            foreach (var t in value) {
                if (La(1) == t) {
                    Consume();
                } else {
                    throw new ParseException("String mismatch");
                }
            }
        }

        /// <summary>
        /// Matches the range.
        /// </summary>
        /// <param name="value">The value.</param>
        protected void MatchRange(char[] value) {
            if (IsInRange(value)) {
                Consume();
            } else {
                throw new ParseException("Character mismatch");
            }
        }

        /// <summary>
        /// Matches the range.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="minOccurs">The min occurs.</param>
        /// <param name="maxOccurs">The max occurs.</param>
        protected void MatchRange(char[] value, int minOccurs, int maxOccurs) {
            var i = 0;
            while (IsInRange(value)) {
                Consume();
                i++;
            }
            ValidateOccurence(i, minOccurs, maxOccurs);
        }

        /// <summary>
        /// Matches the range.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="last">The last.</param>
        protected void MatchRange(char first, char last) {
            if (IsInRange(first, last)) {
                Consume();
            } else {
                throw new ParseException("Character mismatch");
            }
        }

        /// <summary>
        /// Matches the range.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="last">The last.</param>
        /// <param name="minOccurs">The min occurs.</param>
        /// <param name="maxOccurs">The max occurs.</param>
        protected void MatchRange(char first, char last, int minOccurs, int maxOccurs) {
            var i = 0;
            while (IsInRange(first, last)) {
                Consume();
                i++;
            }
            ValidateOccurence(i, minOccurs, maxOccurs);
        }

        /// <summary>
        /// Gets the next token.
        /// </summary>
        /// <returns></returns>
        public abstract Token NextToken();
    }
}
