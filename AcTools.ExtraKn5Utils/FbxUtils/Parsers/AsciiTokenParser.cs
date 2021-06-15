// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.Text;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value;

namespace AcTools.ExtraKn5Utils.FbxUtils.Parsers {
    internal static class AsciiTokenParser {
        public static bool TryParseCommentToken(FbxAsciiFileInfo fbxAsciiFileInfo, out CommentToken commentToken) {
            var c = fbxAsciiFileInfo.PeekChar();

            if (c != ';') {
                commentToken = null;
                return false;
            }

            var stringBuilder = new StringBuilder();
            while (!c.IsLineEnd() && !fbxAsciiFileInfo.IsEndOfStream()) {
                stringBuilder.Append(fbxAsciiFileInfo.ReadChar());
                c = fbxAsciiFileInfo.PeekChar();
            }
            commentToken = new CommentToken(stringBuilder.ToString());
            return true;
        }

        public static bool TryConsumeWhiteSpace(FbxAsciiFileInfo fbxAsciiFileInfo) {
            var c = fbxAsciiFileInfo.PeekChar();

            if (!(char.IsWhiteSpace(c) || c.IsLineEnd())) {
                return false;
            }

            var stringBuilder = new StringBuilder();
            while ((char.IsWhiteSpace(c) || c.IsLineEnd()) && !fbxAsciiFileInfo.IsEndOfStream()) {
                stringBuilder.Append(fbxAsciiFileInfo.ReadChar());
                c = fbxAsciiFileInfo.PeekChar();
            }

            return true;
        }

        public static bool TryParseStringToken(FbxAsciiFileInfo fbxAsciiFileInfo, out StringToken stringToken) {
            var c = fbxAsciiFileInfo.PeekChar();

            if (c != '"') {
                stringToken = null;
                return false;
            }

            fbxAsciiFileInfo.ReadChar();

            var stringBuilder = new StringBuilder();
            while (fbxAsciiFileInfo.PeekChar() != '"') {
                stringBuilder.Append(fbxAsciiFileInfo.ReadChar());
                if (fbxAsciiFileInfo.IsEndOfStream()) {
                    throw new FbxException(fbxAsciiFileInfo, "Unexpected end of stream; expecting end quote");
                }
            }

            fbxAsciiFileInfo.ReadChar();

            stringToken = new StringToken(stringBuilder.ToString());
            return true;
        }

        public static bool TryParseIdentifierOrCharToken(FbxAsciiFileInfo fbxAsciiFileInfo, out Token token) {
            var c = fbxAsciiFileInfo.PeekChar();

            if (!c.IsIdentifierChar()) {
                token = null;
                return false;
            }

            var stringBuilder = new StringBuilder();
            while (c.IsIdentifierChar() && !fbxAsciiFileInfo.IsEndOfStream()) {
                stringBuilder.Append(fbxAsciiFileInfo.ReadChar());
                c = fbxAsciiFileInfo.PeekChar();
            }

            TryConsumeWhiteSpace(fbxAsciiFileInfo);

            var identifier = stringBuilder.ToString();

            if (fbxAsciiFileInfo.PeekChar() == ':') {
                fbxAsciiFileInfo.ReadChar();
                token = new IdentifierToken(identifier);
                return true;
            }

            if (identifier.Equals("T") || identifier.Equals("F") || identifier.Equals("Y") || identifier.Equals("N")) {
                token = new BooleanToken(identifier.Equals("T") || identifier.Equals("Y"));
                return true;
            }

            throw new FbxException(fbxAsciiFileInfo, "Unexpected '" + identifier + "', expected ':' or a single-char literal");
        }

        public static bool TryParseOperatorToken(FbxAsciiFileInfo fbxAsciiFileInfo, out Token operatorToken) {
            var c = fbxAsciiFileInfo.PeekChar();
            if (c.Equals('{')) {
                operatorToken = Token.CreateOpenBrace();
            } else if (c.Equals('}')) {
                operatorToken = Token.CreateCloseBrace();
            } else if (c.Equals('*')) {
                operatorToken = Token.CreateAsterix();
            } else if (c.Equals(',')) {
                operatorToken = Token.CreateComma();
            } else {
                operatorToken = null;
                return false;
            }

            fbxAsciiFileInfo.ReadChar();
            return true;
        }

        public static bool TryParseNumberToken(FbxAsciiFileInfo fbxAsciiFileInfo, out Token numberToken) {
            var c = fbxAsciiFileInfo.PeekChar();

            var isFirst = true;
            if (!c.IsDigit(isFirst)) {
                numberToken = null;
                return false;
            }

            var stringBuilder = new StringBuilder();
            while (c.IsDigit(isFirst) && !fbxAsciiFileInfo.IsEndOfStream()) {
                stringBuilder.Append(fbxAsciiFileInfo.ReadChar());
                isFirst = false;
                c = fbxAsciiFileInfo.PeekChar();
            }

            var value = stringBuilder.ToString();
            if (!value.TryParseNumberToken(out numberToken)) {
                throw new FbxException(fbxAsciiFileInfo, $"Invalid number '{value}'");
            }

            return true;
        }
    }
}