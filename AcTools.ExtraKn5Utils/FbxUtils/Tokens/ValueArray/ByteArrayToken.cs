// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.IO;

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens.ValueArray {
    public class ByteArrayToken : Token {
        public byte[] Values { get; set; }

        internal override void WriteBinary(FbxVersion version, BinaryWriter binaryWriter) {
            binaryWriter.Write((byte)'R');
            binaryWriter.Write(Values.Length);
            binaryWriter.Write(Values);
        }

        internal override void WriteAscii(FbxVersion version, LineStringBuilder lineStringBuilder, int indentLevel) {
            var arrayLength = Values.Length;
            WriteAsciiArray(version, lineStringBuilder, arrayLength, indentLevel, (itemWriter) => {
                for (var i = 0; i < Values.Length; i++) {
                    if (i > 0) {
                        lineStringBuilder.Append(",");
                    }
                    lineStringBuilder.Append(Values[i].ToString());
                }
            });
        }

        public ByteArrayToken(byte[] values) : base(TokenType.ValueArray, ValueType.Byte) {
            Values = values;
        }
    }
}