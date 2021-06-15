// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.FbxUtils.Parsers;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.ValueArray;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Reads FBX nodes from a text stream
    /// </summary>
    public class FbxAsciiReader {
        private readonly FbxAsciiFileInfo _fbxAsciiFileInfo;
        private readonly ErrorLevel _errorLevel;
        private readonly Stack<Token> _tokenStack;

        /// <summary>
        /// Creates a new reader
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="errorLevel"></param>
        public FbxAsciiReader(Stream stream, ErrorLevel errorLevel = ErrorLevel.Checked) {
            _fbxAsciiFileInfo = new FbxAsciiFileInfo(stream ?? throw new ArgumentNullException(nameof(stream)));
            _errorLevel = errorLevel;
            _tokenStack = new Stack<Token>();
        }

        /// <summary>
        /// The maximum array size that will be allocated
        /// </summary>
        /// <remarks>
        /// If you trust the source, you can expand this value as necessary.
        /// Malformed files could cause large amounts of memory to be allocated
        /// and slow or crash the system as a result.
        /// </remarks>
        public int MaxArrayLength { get; set; } = (1 << 24);

        Token ReadToken() {
            while (_tokenStack.Count == 0) {
                if (_fbxAsciiFileInfo.IsEndOfStream()) {
                    _tokenStack.Push(Token.CreateEndOfStream());
                } else if (AsciiTokenParser.TryConsumeWhiteSpace(_fbxAsciiFileInfo)) {
                    continue;
                } else if (AsciiTokenParser.TryParseCommentToken(_fbxAsciiFileInfo, out var _)) {
                    continue;
                } else if (AsciiTokenParser.TryParseOperatorToken(_fbxAsciiFileInfo, out var operatorToken)) {
                    _tokenStack.Push(operatorToken);
                } else if (AsciiTokenParser.TryParseNumberToken(_fbxAsciiFileInfo, out var numberToken)) {
                    _tokenStack.Push(numberToken);
                } else if (AsciiTokenParser.TryParseStringToken(_fbxAsciiFileInfo, out var stringToken)) {
                    _tokenStack.Push(stringToken);
                } else if (AsciiTokenParser.TryParseIdentifierOrCharToken(_fbxAsciiFileInfo, out var token)) {
                    _tokenStack.Push(token);
                } else {
                    throw new FbxException(_fbxAsciiFileInfo, $"Unknown character {_fbxAsciiFileInfo.PeekChar()}");
                }
            }
            return _tokenStack.Pop();
        }

        void ExpectToken(Token token) {
            var t = ReadToken();
            if (!token.Equals(t)) {
                throw new FbxException(_fbxAsciiFileInfo, "Unexpected '" + t + "', expected " + token);
            }
        }

        private enum ArrayType {
            Byte = 0,
            Integer = 1,
            Long = 2,
            Float = 3,
            Double = 4,
        };

        Token ReadArray() {
            // Read array length and header
            var len = ReadToken();

            if (!len.TryGetAsLong(out var arrayLength)) {
                throw new FbxException(_fbxAsciiFileInfo, "Unexpected token type '" + len.TokenType + "', expected an integer");
            }

            if (arrayLength < 0) {
                throw new FbxException(_fbxAsciiFileInfo, "Invalid array length " + arrayLength);
            }

            if (arrayLength > MaxArrayLength) {
                throw new FbxException(_fbxAsciiFileInfo, "Array length " + arrayLength + " higher than permitted maximum " + MaxArrayLength);
            }

            ExpectToken(Token.CreateOpenBrace());
            ExpectToken(new IdentifierToken("a"));
            var array = new List<double>();

            // Read array elements
            bool expectComma = false;
            Token token = ReadToken();
            var arrayType = ArrayType.Byte;

            while (token.TokenType != TokenType.CloseBrace) {
                if (expectComma) {
                    if (token.TokenType != TokenType.Comma) {
                        throw new FbxException(_fbxAsciiFileInfo, "Unexpected '" + token + "', expected ','");
                    }
                    expectComma = false;
                    token = ReadToken();
                    continue;
                }
                if (array.Count > arrayLength) {
                    if (_errorLevel >= ErrorLevel.Checked) {
                        throw new FbxException(_fbxAsciiFileInfo, "Too many elements in array");
                    }
                    token = ReadToken();
                    continue;
                }

                if (token.TryGetAsDouble(out var value)) {
                    if (token.ValueType == Tokens.ValueType.Integer && arrayType < ArrayType.Integer) {
                        arrayType = ArrayType.Integer;
                    } else if (token.ValueType == Tokens.ValueType.Long && arrayType < ArrayType.Long) {
                        arrayType = ArrayType.Long;
                    } else if (token.ValueType == Tokens.ValueType.Float) {
                        arrayType = arrayType < ArrayType.Long ? ArrayType.Float : ArrayType.Double;
                    } else if (token.ValueType == Tokens.ValueType.Double && arrayType < ArrayType.Double) {
                        arrayType = ArrayType.Double;
                    }
                    array.Add(value);
                } else {
                    throw new FbxException(_fbxAsciiFileInfo, "Unexpected '" + token.TokenType + "', expected a value");
                }

                expectComma = true;
                token = ReadToken();
            }

            if (array.Count < arrayLength && _errorLevel >= ErrorLevel.Checked) {
                throw new FbxException(_fbxAsciiFileInfo, "Too few elements in array - expected " + (arrayLength - array.Count) + " more");
            }

            // Convert the array to the smallest type we can see
            Token ret;
            switch (arrayType) {
                case ArrayType.Byte:
                    var byteArray = (from item in array select (byte)item).ToArray();
                    ret = new ByteArrayToken(byteArray);
                    break;
                case ArrayType.Integer:
                    var integerArray = (from item in array select (int)item).ToArray();
                    ret = new IntegerArrayToken(integerArray);
                    break;
                case ArrayType.Long:
                    var longArray = (from item in array select (long)item).ToArray();
                    ret = new LongArrayToken(longArray);
                    break;
                case ArrayType.Float:
                    var floatArray = (from item in array select (float)item).ToArray();
                    ret = new FloatArrayToken(floatArray);
                    break;
                default:
                    ret = new DoubleArrayToken(array.ToArray());
                    break;
            }
            return ret;
        }

        /// <summary>
        /// Reads the next node from the stream
        /// </summary>
        /// <returns>The read node, or <c>null</c></returns>
        public FbxNode ReadNode() {
            var first = ReadToken();
            if (!(first is IdentifierToken id)) {
                if (first is Token tok && tok.TokenType == TokenType.EndOfStream) {
                    return null;
                }
                throw new FbxException(_fbxAsciiFileInfo, "Unexpected '" + first + "', expected an identifier");
            }
            var node = new FbxNode(id);

            // Read properties
            Token token = ReadToken();
            bool expectComma = false;
            while (token.TokenType != TokenType.OpenBrace && token.TokenType != TokenType.Identifier && token.TokenType != TokenType.CloseBrace) {
                if (expectComma) {
                    if (token.TokenType != TokenType.Comma) {
                        throw new FbxException(_fbxAsciiFileInfo, "Unexpected '" + token + "', expected a ','");
                    }
                    expectComma = false;
                    token = ReadToken();
                    continue;
                }

                if (token.TokenType == TokenType.Asterix) {
                    token = ReadArray();
                } else if (token.TokenType == TokenType.CloseBrace || token.TokenType == TokenType.Comma) {
                    throw new FbxException(_fbxAsciiFileInfo, "Unexpected '" + token.TokenType + "' in property list");
                }

                node.AddProperty(token);
                expectComma = true; // The final comma before the open brace isn't required
                token = ReadToken();
            }
            // TODO: Merge property list into an array as necessary
            // Now we're either at an open brace, close brace or a new node
            if (token.TokenType == TokenType.Identifier || token.TokenType == TokenType.CloseBrace) {
                _tokenStack.Push(token);
                return node;
            }
            // The while loop can't end unless we're at an open brace, so we can continue right on
            Token endBrace = ReadToken();
            while (endBrace.TokenType != TokenType.CloseBrace) {
                _tokenStack.Push(endBrace);
                node.AddNode(ReadNode());
                endBrace = ReadToken();
            }
            if (node.Nodes.Count < 1) // If there's an open brace, we want that to be preserved
            {
                node.AddNode(null);
            }

            return node;
        }

        /// <summary>
        /// Reads a full document from the stream
        /// </summary>
        /// <returns>The complete document object</returns>
        public FbxDocument Read() {
            var ret = new FbxDocument();

            // Read version string
            const string versionString = @"; FBX (\d)\.(\d)\.(\d) project file";

            AsciiTokenParser.TryConsumeWhiteSpace(_fbxAsciiFileInfo);

            bool hasVersionString = false;
            if (AsciiTokenParser.TryParseCommentToken(_fbxAsciiFileInfo, out var commentToken)) {
                var match = Regex.Match(commentToken.Value, versionString);
                hasVersionString = match.Success;
                if (hasVersionString) {
                    ret.Version = (FbxVersion)(
                            int.Parse(match.Groups[1].Value) * 1000 +
                                    int.Parse(match.Groups[2].Value) * 100 +
                                    int.Parse(match.Groups[3].Value) * 10
                            );
                }
            }

            if (!hasVersionString && _errorLevel >= ErrorLevel.Strict) {
                throw new FbxException(_fbxAsciiFileInfo, "Invalid version string; first line must match \"" + versionString + "\"");
            }

            FbxNode node;
            while ((node = ReadNode()) != null) {
                ret.AddNode(node);
            }

            return ret;
        }
    }
}