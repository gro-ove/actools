using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class ControllerButtonCombined : NotifyPropertyChanged {
        public ControllerButtonCombined([LocalizationRequired(false)] string id, string displayName, bool isNew = false) {
            IsNew = isNew;
            ControllerButton = new ControllerButtonEntry(id, displayName);
            KeyboardButton = new KeyboardButtonEntry(id, displayName);
        }

        public ControllerButtonEntry ControllerButton { get; }
        public KeyboardButtonEntry KeyboardButton { get; }
        public bool IsNew { get; }
    }
}