// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.IO;

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value {
    public class ShortToken : Token {
        public short Value { get; set; }

        internal override void WriteBinary(FbxVersion version, BinaryWriter binaryWriter) {
            binaryWriter.Write((byte)('Y'));
            binaryWriter.Write(Value);
        }

        internal override void WriteAscii(FbxVersion version, LineStringBuilder lineStringBuilder, int indentLevel) {
            lineStringBuilder.Append(Value.ToString());
        }

        public override bool Equals(object obj) {
            if (obj is LongToken id) {
                return Value == id.Value;
            }
            return false;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public ShortToken(short value) : base(TokenType.Value, ValueType.Short) {
            Value = value;
        }
    }
}