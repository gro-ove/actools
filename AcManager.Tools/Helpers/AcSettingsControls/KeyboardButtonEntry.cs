using System.Collections.Generic;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;

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

        public override void Load(IniFile ini, IReadOnlyList<DirectInputDevice> devices) {
            var section = ini[Id];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt("KEY", -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set("KEY", Input == null ? "-1" : "0x" + Input?.Id.ToString("X") + " ; " + Input.DisplayName);
        }
    }
}