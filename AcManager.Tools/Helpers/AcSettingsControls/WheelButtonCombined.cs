using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class WheelButtonCombined : NotifyPropertyChanged {
        public WheelButtonCombined([LocalizationRequired(false)] string id, string displayName, bool isNew = false) {
            IsNew = isNew;
            WheelButton = new WheelButtonEntry(id, displayName);
            KeyboardButton = new KeyboardButtonEntry(id, displayName);
        }

        public WheelButtonEntry WheelButton { get; }
        public KeyboardButtonEntry KeyboardButton { get; }
        public bool IsNew { get; }
    }
}