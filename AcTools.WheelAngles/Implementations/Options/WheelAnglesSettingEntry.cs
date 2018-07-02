using System.ComponentModel;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations.Options {
    public class WheelAnglesSettingEntry : Displayable, IWithId {
        public WheelAnglesSettingEntry([LocalizationRequired(false)] string value, string displayName) {
            DisplayName = displayName;
            Value = value;
        }

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }

        [Localizable(false)]
        public string Value { get; }

        [Localizable(false)]
        public string Id => Value;
    }
}