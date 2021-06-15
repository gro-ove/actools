// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.IO;
using System.Text;

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens {
    public class StringToken : Token {
        public readonly string Value;

        internal override void WriteBinary(FbxVersion version, BinaryWriter binaryWriter) {
            const string asciiSeparator = "::";
            const string binarySeparator = "\0\x1";

            binaryWriter.Write((byte)('S'));

            var str = Value;
            if (str.Contains(asciiSeparator)) {
                var tokens = str.Split(new[] { asciiSeparator }, StringSplitOptions.None);
                var sb = new StringBuilder();
                bool first = true;
                for (int i = tokens.Length - 1; i >= 0; i--) {
                    if (!first) {
                        sb.Append(binarySeparator);
                    }
                    sb.Append(tokens[i]);
                    first = false;
                }
                str = sb.ToString();
            }
            var bytes = Encoding.ASCII.GetBytes(str);
            binaryWriter.Write(bytes.Length);
            binaryWriter.Write(bytes);
        }

        internal override void WriteAscii(FbxVersion version, LineStringBuilder lineStringBuilder, int indentLevel) {
            lineStringBuilder.Append($"\"{Value}\"");
        }

        public StringToken(string value) : base(TokenType.String, ValueType.None) {
            Value = value;
        }
    }
}