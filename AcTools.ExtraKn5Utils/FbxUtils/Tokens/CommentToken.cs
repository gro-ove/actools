// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens {
    public class CommentToken : Token {
        public readonly string Value;

        public CommentToken(string value) : base(TokenType.Comment, ValueType.None) {
            Value = value;
        }
    }
}