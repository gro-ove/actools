using AcManager.Tools.Helpers.DirectInput;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public sealed class KeyboardButtonEntry : BaseEntry<KeyboardInputButton> {
        public KeyboardButtonEntry(string id, string name) : base(id, name) {}

        protected override void OnInputChanged(KeyboardInputButton oldValue, KeyboardInputButton newValue) {
            if (oldValue != null) {
                oldValue.Used--;
            }

            if (newValue != null) {
                newValue.Used++;
            }
        }
    }
}