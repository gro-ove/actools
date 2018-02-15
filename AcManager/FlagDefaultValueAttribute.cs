using System;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace AcManager {
    internal class FlagDefaultValueAttribute : Attribute {
        public string Value { get; }

        public FlagDefaultValueAttribute([Localizable(false)] string value) {
            Value = value;
        }

        [CanBeNull]
        public static string GetValue(Enum enumVal) {
            return enumVal.GetType().GetMember(enumVal.ToString())[0]
                    .GetCustomAttributes(typeof(FlagDefaultValueAttribute), false)
                    .OfType<FlagDefaultValueAttribute>().FirstOrDefault()?.Value;
        }
    }
}