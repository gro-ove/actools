// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.IO;

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value {
    public class IntegerToken : Token {
        public int Value { get; set; }

        internal override void WriteBinary(FbxVersion version, BinaryWriter binaryWriter) {
            binaryWriter.Write((byte)('I'));
            binaryWriter.Write(Value);
        }

        internal override void WriteAscii(FbxVersion version, LineStringBuilder lineStringBuilder, int indentLevel) {
            lineStringBuilder.Append(Value.ToString());
        }

        public override bool Equals(object obj) {
            if (obj is IntegerToken id) {
                return Value == id.Value;
            }
            return false;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public IntegerToken(int value) : base(TokenType.Value, ValueType.Integer) {
            Value = value;
        }
    }
}