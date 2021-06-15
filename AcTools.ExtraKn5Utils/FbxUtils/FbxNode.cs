// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.Collections.Generic;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Represents a node in an FBX file
    /// </summary>
    public class FbxNode : FbxNodeList {
        public readonly List<Token> Properties = new List<Token>();

        /// <summary>
        /// The node name, which is often a class type
        /// </summary>
        /// <remarks>
        /// The name must be smaller than 256 characters to be written to a binary stream
        /// </remarks>
        public IdentifierToken Identifier { get; }

        public FbxNode(IdentifierToken identifier) {
            Identifier = identifier;
        }

        public Token GetPropertyWithName(string name) {
            foreach (var property in Properties) {
                if (property.TokenType != TokenType.String) {
                    continue;
                }
                var stringToken = (StringToken)property;
                var propertyName = stringToken.Value?.Split(new string[] { "::" }, StringSplitOptions.None)[0];
                if (string.Equals(propertyName, name, StringComparison.CurrentCultureIgnoreCase)) {
                    return property;
                }
            }
            return null;
        }

        public string GetName(string type) {
            return GetPropertyWithName(type).GetAsString().Split(new[] { "::" }, 2, StringSplitOptions.None)[1];
        }

        public void AddProperty(Token value) {
            Properties.Add(value);
        }

        public int[] PropertiesToIntArray() {
            var values = new List<int>();
            foreach (var property in Properties) {
                if (property.TokenType != TokenType.Value && property.ValueType != Tokens.ValueType.None) {
                    continue;
                }
                if (property.ValueType == Tokens.ValueType.Boolean && property is BooleanToken booleanToken) {
                    values.Add(booleanToken.Value ? 1 : 0);
                } else if (property.ValueType == Tokens.ValueType.Integer && property is IntegerToken integerToken) {
                    values.Add(integerToken.Value);
                } else if (property.ValueType == Tokens.ValueType.Long && property is LongToken longToken) {
                    values.Add((int)longToken.Value);
                } else if (property.ValueType == Tokens.ValueType.Float && property is FloatToken floatToken) {
                    values.Add((int)floatToken.Value);
                } else if (property.ValueType == Tokens.ValueType.Double && property is DoubleToken doubleToken) {
                    values.Add((int)doubleToken.Value);
                }
            }
            return values.ToArray();
        }

        public float[] PropertiesToFloatArray() {
            var values = new List<float>();
            foreach (var property in Properties) {
                if (property.TokenType != TokenType.Value && property.ValueType != Tokens.ValueType.None) {
                    continue;
                }
                if (property.ValueType == Tokens.ValueType.Boolean && property is BooleanToken booleanToken) {
                    values.Add(booleanToken.Value ? 1 : 0);
                } else if (property.ValueType == Tokens.ValueType.Integer && property is IntegerToken integerToken) {
                    values.Add(integerToken.Value);
                } else if (property.ValueType == Tokens.ValueType.Long && property is LongToken longToken) {
                    values.Add(longToken.Value);
                } else if (property.ValueType == Tokens.ValueType.Float && property is FloatToken floatToken) {
                    values.Add(floatToken.Value);
                } else if (property.ValueType == Tokens.ValueType.Double && property is DoubleToken doubleToken) {
                    values.Add((float)doubleToken.Value);
                }
            }
            return values.ToArray();
        }

        /// <summary>
        /// The first property element
        /// </summary>
        public Token Value {
            get { return Properties.Count < 1 ? null : Properties[0]; }
            set {
                if (Properties.Count < 1) {
                    Properties.Add(value);
                } else {
                    Properties[0] = value;
                }
            }
        }

        /// <summary>
        /// Whether the node is empty of data
        /// </summary>
        public bool IsEmpty => string.IsNullOrEmpty(Identifier.Value) && Properties.Count == 0 && Nodes.Count == 0;
    }
}