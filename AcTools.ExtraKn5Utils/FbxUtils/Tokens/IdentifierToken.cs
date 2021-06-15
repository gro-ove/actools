// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.IO;
using System.Text;

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens {
    public class IdentifierToken : Token {
        public readonly string Value;

        internal override void WriteBinary(FbxVersion version, BinaryWriter binaryWriter) {
            var bytes = Encoding.ASCII.GetBytes(Value ?? string.Empty);
            if (bytes.Length > byte.MaxValue) {
                throw new FbxException(binaryWriter.BaseStream.Position, "Identifier value is too long");
            }
            binaryWriter.Write((byte)bytes.Length);
            if (bytes.Length > 0) {
                binaryWriter.Write(bytes);
            }
        }

        internal override void WriteAscii(FbxVersion version, LineStringBuilder lineStringBuilder, int indentLevel) {
            lineStringBuilder.Append($"{Value}:");
        }

        public override bool Equals(object obj) {
            if (obj is IdentifierToken id) {
                return Value == id.Value;
            }
            return false;
        }

        public override int GetHashCode() {
            return Value?.GetHashCode() ?? 0;
        }

        public IdentifierToken(string value) : base(TokenType.Identifier, ValueType.None) {
            Value = value;
        }
    }
}