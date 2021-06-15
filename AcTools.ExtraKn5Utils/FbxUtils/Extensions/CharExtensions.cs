// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

namespace AcTools.ExtraKn5Utils.FbxUtils.Extensions {
    public static class CharExtensions {
        public static bool IsLineFeed(this char c) {
            return c == '\n';
        }

        public static bool IsCarriageReturn(this char c) {
            return c == '\r';
        }

        public static bool IsLineEnd(this char c) {
            return c.IsCarriageReturn() || c.IsLineFeed();
        }

        public static bool IsIdentifierChar(this char c) {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        public static bool IsDigit(this char c, bool first) {
            if (char.IsDigit(c)) {
                return true;
            }

            switch (c) {
                case '-':
                case '+':
                    return true;
                case '.':
                case 'e':
                case 'E':
                case 'X':
                case 'x':
                    return !first;
            }
            return false;
        }
    }
}