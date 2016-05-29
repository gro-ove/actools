using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public sealed class SettingEntry : Displayable, IWithId {
        public SettingEntry(string value, string displayName) {
            DisplayName = displayName;
            Value = value;
        }

        public string Value { get; }

        public string Id => Value;
    }
}