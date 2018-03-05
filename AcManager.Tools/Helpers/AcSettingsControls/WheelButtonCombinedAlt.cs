using System.ComponentModel;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class WheelButtonCombinedAlt : WheelButtonCombined {
        public WheelButtonCombinedAlt([LocalizationRequired(false)] string id, string displayName, bool isNew = false)
                : base(id, displayName, isNew) {
            WheelButtonAlt = new WheelButtonAltEntry(id, displayName);
            KeyboardButton.SubscribeWeak(OnKeyboardInputChanged);
        }

        private void OnKeyboardInputChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(KeyboardButton.Input)) {
                WheelButtonAlt.IsAvailable = KeyboardButton.Input != null;
            }
        }

        public WheelButtonAltEntry WheelButtonAlt { get; }
    }
}