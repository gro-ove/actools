// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value;

namespace AcTools.ExtraKn5Utils.FbxUtils.Tokens {
    public enum TokenType {
        EndOfStream,
        Comment,
        OpenBrace,
        CloseBrace,
        Comma,
        Asterix,
        Identifier,
        String,
        Value,
        ValueArray
    }

    public enum ValueType {
        None,
        Boolean,
        Byte, // valid for array only
        Short, //not valid for array
        Integer,
        Long,
        Float,
        Double
    }

    public class Token : IEqualityComparer<Token> {
        public TokenType TokenType { get; }

        public ValueType ValueType { get; }

        internal virtual void WriteBinary(FbxVersion version, BinaryWriter binaryWriter) {
            throw new NotImplementedException();
        }

        internal virtual void WriteAscii(FbxVersion version, LineStringBuilder lineStringBuilder, int indentLevel) {
            throw new NotImplementedException();
        }

        internal void WriteBinaryArray(BinaryWriter stream, int uncompressedSize, Action<BinaryWriter> itemWriterAction) {
            bool compress = uncompressedSize >= Settings.CompressionThreshold;

            stream.Write(compress ? 1 : 0);

            if (compress) {
                var compressLengthPos = stream.BaseStream.Position;
                stream.Write(0);
                var dataStart = stream.BaseStream.Position;
                stream.Write(new byte[] { 0x58, 0x85 }, 0, 2);

                uint checksum;
                using (var deflateStream = new DeflateStream(stream.BaseStream, CompressionMode.Compress, true))
                using (var checksumBinaryWriter = new ChecksumBinaryWriter(deflateStream)) {
                    itemWriterAction.Invoke(checksumBinaryWriter);
                    checksum = checksumBinaryWriter.Checksum;
                }

                var checksumBytes = new byte[] {
                    (byte)((checksum >>  24) & 0xFF),
                    (byte)((checksum >>  16) & 0xFF),
                    (byte)((checksum >>  8) & 0xFF),
                    (byte)(checksum & 0xFF),
                };
                stream.Write(checksumBytes);

                var dataEnd = stream.BaseStream.Position;
                stream.BaseStream.Position = compressLengthPos;
                var compressedSize = (int)(dataEnd - dataStart);
                stream.Write(compressedSize);
                stream.BaseStream.Position = dataEnd;
            } else {
                stream.Write(uncompressedSize);
                itemWriterAction.Invoke(stream);
            }
        }

        internal void WriteAsciiArray(FbxVersion version, LineStringBuilder lineStringBuilder, int arrayLength, int indentLevel,
                Action<LineStringBuilder> itemWriterAction) {
            if (version >= FbxVersion.v7_1) {
                lineStringBuilder.Append("*").Append(arrayLength.ToString()).Append(" {\n");
                lineStringBuilder.Indent(indentLevel + 1);
                lineStringBuilder.Append("a: ");
            }

            itemWriterAction.Invoke(lineStringBuilder);

            if (version >= FbxVersion.v7_1) {
                lineStringBuilder.Append("\n");
                lineStringBuilder.Indent(indentLevel);
                lineStringBuilder.Append("}");
            }
        }

        public bool Equals(Token other) {
            if (other != null) {
                if (this is BooleanToken booleanToken && other is BooleanToken booleanTokenOther) {
                    return booleanToken.Value == booleanTokenOther.Value;
                }
                if (this is ShortToken shortToken && other is ShortToken shortTokenOther) {
                    return shortToken.Value == shortTokenOther.Value;
                }
                if (this is IntegerToken integerToken && other is IntegerToken integerTokenOther) {
                    return integerToken.Value == integerTokenOther.Value;
                }
                if (this is LongToken longToken && other is LongToken longTokenOther) {
                    return longToken.Value == longTokenOther.Value;
                }
                if (this is FloatToken floatToken && other is FloatToken floatTokenOther) {
                    return floatToken.Value == floatTokenOther.Value;
                }
                if (this is DoubleToken doubleToken && other is DoubleToken doubleTokenOther) {
                    return doubleToken.Value == doubleTokenOther.Value;
                }
                if (this is StringToken stringToken && other is StringToken stringTokenOther) {
                    return stringToken.Value == stringTokenOther.Value;
                }
                if (this is CommentToken commentToken && other is CommentToken commentTokenOther) {
                    return commentToken.Value == commentTokenOther.Value;
                }
                if (this is IdentifierToken identifierToken && other is IdentifierToken identifierTokenOther) {
                    return identifierToken.Value == identifierTokenOther.Value;
                }
                if (TokenType == other.TokenType && TokenType != TokenType.ValueArray && ValueType == other.ValueType) {
                    return true;
                }
            }
            return false;
        }

        public static Token CreateAsterix() {
            return new Token(TokenType.Asterix);
        }

        public static Token CreateComma() {
            return new Token(TokenType.Comma);
        }

        public static Token CreateOpenBrace() {
            return new Token(TokenType.OpenBrace);
        }

        public static Token CreateCloseBrace() {
            return new Token(TokenType.CloseBrace);
        }

        public static Token CreateEndOfStream() {
            return new Token(TokenType.EndOfStream);
        }

        public bool Equals(Token x, Token y) {
            throw new NotImplementedException();
        }

        public int GetHashCode(Token obj) {
            throw new NotImplementedException();
        }

        internal Token(TokenType tokenType) {
            TokenType = tokenType;
            ValueType = ValueType.None;
        }

        internal Token(TokenType tokenType, ValueType valueType) {
            TokenType = tokenType;
            ValueType = valueType;
        }
    }
}