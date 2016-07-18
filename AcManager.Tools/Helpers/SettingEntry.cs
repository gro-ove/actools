using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public sealed class SettingEntry : Displayable, IWithId {
        public SettingEntry([LocalizationRequired(false)] string value, string displayName) {
            DisplayName = displayName;
            Value = value;
            IntValue = value.ToInvariantInt();
        }

        public SettingEntry(int value, string displayName) {
            DisplayName = displayName;
            Value = value.ToInvariantString();
            IntValue = value;
        }

        public string Value { get; }

        public int? IntValue { get; }

        public string Id => Value;
    }
}