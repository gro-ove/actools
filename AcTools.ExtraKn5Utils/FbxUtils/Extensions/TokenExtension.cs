// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.Linq;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.ValueArray;

namespace AcTools.ExtraKn5Utils.FbxUtils.Extensions {
    public static class TokenExtension {
        public static string GetAsString(this Token token) {
            if (!TryGetAsString(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsString(this Token token, out string result) {
            if (token.ValueType == Tokens.ValueType.None) {
                if (token is StringToken stringToken) {
                    result = stringToken.Value;
                    return true;
                }
            } else if (token.TokenType == TokenType.Value && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanToken booleanToken:
                        result = booleanToken.Value ? "T" : "F";
                        return true;
                    case ShortToken shortToken:
                        result = shortToken.Value.ToString();
                        return true;
                    case IntegerToken integerToken:
                        result = integerToken.Value.ToString();
                        return true;
                    case LongToken longToken:
                        result = longToken.Value.ToString();
                        return true;
                    case FloatToken floatToken:
                        result = floatToken.Value.ToString();
                        return true;
                    case DoubleToken doubleToken:
                        result = doubleToken.Value.ToString();
                        return true;
                }
            }
            result = null;
            return false;
        }

        public static float GetAsFloat(this Token token) {
            if (!TryGetAsFloat(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsFloat(this Token token, out float result) {
            if (token.TokenType == TokenType.Value && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanToken booleanToken:
                        result = booleanToken.Value ? 1 : 0;
                        return true;
                    case ShortToken shortToken:
                        result = shortToken.Value;
                        return true;
                    case IntegerToken integerToken:
                        result = integerToken.Value;
                        return true;
                    case LongToken longToken:
                        result = longToken.Value;
                        return true;
                    case FloatToken floatToken:
                        result = floatToken.Value;
                        return true;
                    case DoubleToken doubleToken:
                        result = (float)doubleToken.Value;
                        return true;
                }
            }
            result = 0;
            return false;
        }

        public static double GetAsDouble(this Token token) {
            if (!TryGetAsDouble(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsDouble(this Token token, out double result) {
            if (token.TokenType == TokenType.Value && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanToken booleanToken:
                        result = booleanToken.Value ? 1 : 0;
                        return true;
                    case ShortToken shortToken:
                        result = shortToken.Value;
                        return true;
                    case IntegerToken integerToken:
                        result = integerToken.Value;
                        return true;
                    case LongToken longToken:
                        result = longToken.Value;
                        return true;
                    case FloatToken floatToken:
                        result = floatToken.Value;
                        return true;
                    case DoubleToken doubleToken:
                        result = doubleToken.Value;
                        return true;
                }
            }
            result = 0;
            return false;
        }

        public static double[] GetAsDoubleArray(this Token token) {
            if (!TryGetAsDoubleArray(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsDoubleArray(this Token token, out double[] result) {
            if (token.TokenType == TokenType.ValueArray && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanArrayToken booleanArrayToken:
                        result = (from item in booleanArrayToken.Values select (double)(item ? 1 : 0)).ToArray();
                        return true;
                    case ByteArrayToken byteArrayToken:
                        result = (from item in byteArrayToken.Values select (double)item).ToArray();
                        return true;
                    case IntegerArrayToken integerArrayToken:
                        result = (from item in integerArrayToken.Values select (double)item).ToArray();
                        return true;
                    case LongArrayToken longArrayToken:
                        result = (from item in longArrayToken.Values select (double)item).ToArray();
                        return true;
                    case FloatArrayToken floatArrayToken:
                        result = (from item in floatArrayToken.Values select (double)item).ToArray();
                        return true;
                    case DoubleArrayToken doubleArrayToken:
                        result = doubleArrayToken.Values;
                        return true;
                }
            }
            result = null;
            return false;
        }

        public static long GetAsLong(this Token token) {
            if (!TryGetAsLong(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsLong(this Token token, out long result) {
            if (token.TokenType == TokenType.Value && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanToken booleanToken:
                        result = booleanToken.Value ? 1 : 0;
                        return true;
                    case ShortToken shortToken:
                        result = shortToken.Value;
                        return true;
                    case IntegerToken integerToken:
                        result = integerToken.Value;
                        return true;
                    case LongToken longToken:
                        result = longToken.Value;
                        return true;
                    case FloatToken floatToken:
                        result = (long)floatToken.Value;
                        return true;
                    case DoubleToken doubleToken:
                        result = (long)doubleToken.Value;
                        return true;
                }
            }
            result = 0;
            return false;
        }

        public static int GetAsIntegr(this Token token) {
            if (!TryGetAsInteger(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsInteger(this Token token, out int result) {
            if (token.TokenType == TokenType.Value && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanToken booleanToken:
                        result = booleanToken.Value ? 1 : 0;
                        return true;
                    case ShortToken shortToken:
                        result = shortToken.Value;
                        return true;
                    case IntegerToken integerToken:
                        result = integerToken.Value;
                        return true;
                    case LongToken longToken:
                        result = (int)longToken.Value;
                        return true;
                    case FloatToken floatToken:
                        result = (int)floatToken.Value;
                        return true;
                    case DoubleToken doubleToken:
                        result = (int)doubleToken.Value;
                        return true;
                }
            }
            result = 0;
            return false;
        }

        public static int[] GetAsIntArray(this Token token) {
            if (!TryGetAsIntArray(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsIntArray(this Token token, out int[] result) {
            if (token.TokenType == TokenType.ValueArray && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanArrayToken booleanArrayToken:
                        result = (from item in booleanArrayToken.Values select item ? 1 : 0).ToArray();
                        return true;
                    case ByteArrayToken byteArrayToken:
                        result = (from item in byteArrayToken.Values select (int)item).ToArray();
                        return true;
                    case IntegerArrayToken integerArrayToken:
                        result = integerArrayToken.Values.ToArray();
                        return true;
                    case LongArrayToken longArrayToken:
                        result = (from item in longArrayToken.Values select (int)item).ToArray();
                        return true;
                    case FloatArrayToken floatArrayToken:
                        result = (from item in floatArrayToken.Values select (int)item).ToArray();
                        return true;
                    case DoubleArrayToken doubleArrayToken:
                        result = (from item in doubleArrayToken.Values select (int)item).ToArray();
                        return true;
                }
            }
            result = null;
            return false;
        }

        public static float[] GetAsFloatArray(this Token token) {
            if (!TryGetAsFloatArray(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsFloatArray(this Token token, out float[] result) {
            if (token.TokenType == TokenType.ValueArray && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanArrayToken booleanArrayToken:
                        var booleanValues = booleanArrayToken.Values.ToArray();
                        result = (from item in booleanValues select (float)(item ? 1 : 0)).ToArray();
                        return true;
                    case ByteArrayToken byteArrayToken:
                        var byteValues = byteArrayToken.Values.ToArray();
                        result = (from item in byteValues select (float)item).ToArray();
                        return true;
                    case IntegerArrayToken integerArrayToken:
                        var integerValues = integerArrayToken.Values.ToArray();
                        result = (from item in integerValues select (float)item).ToArray();
                        return true;
                    case LongArrayToken longArrayToken:
                        var longValues = longArrayToken.Values.ToArray();
                        result = (from item in longValues select (float)item).ToArray();
                        return true;
                    case FloatArrayToken floatArrayToken:
                        result = floatArrayToken.Values.ToArray();
                        return true;
                    case DoubleArrayToken doubleArrayToken:
                        var doubleValues = doubleArrayToken.Values.ToArray();
                        result = (from item in doubleValues select (float)item).ToArray();
                        return true;
                }
            }
            result = null;
            return false;
        }

        public static long[] GetAsLongArray(this Token token) {
            if (!TryGetAsLongArray(token, out var result)) {
                throw new NotSupportedException();
            }
            return result;
        }

        public static bool TryGetAsLongArray(this Token token, out long[] result) {
            if (token.TokenType == TokenType.ValueArray && token.ValueType != Tokens.ValueType.None) {
                switch (token) {
                    case BooleanArrayToken booleanArrayToken:
                        var booleanValues = booleanArrayToken.Values.ToArray();
                        result = (from item in booleanValues select (long)(item ? 1 : 0)).ToArray();
                        return true;
                    case ByteArrayToken byteArrayToken:
                        var byteValues = byteArrayToken.Values.ToArray();
                        result = (from item in byteValues select (long)item).ToArray();
                        return true;
                    case IntegerArrayToken integerArrayToken:
                        var integerValues = integerArrayToken.Values.ToArray();
                        result = (from item in integerValues select (long)item).ToArray();
                        return true;
                    case LongArrayToken longArrayToken:
                        result = longArrayToken.Values.ToArray();
                        return true;
                    case FloatArrayToken floatArrayToken:
                        var floatValues = floatArrayToken.Values.ToArray();
                        result = (from item in floatValues select (long)item).ToArray();
                        return true;
                    case DoubleArrayToken doubleArrayToken:
                        var doubleValues = doubleArrayToken.Values.ToArray();
                        result = (from item in doubleValues select (long)item).ToArray();
                        return true;
                }
            }
            result = null;
            return false;
        }
    }
}