using System.Collections.Generic;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class KeyboardButtonEntry : BaseEntry<KeyboardInputButton> {
        public KeyboardButtonEntry([LocalizationRequired(false)] string id, string name) : base(id, name) {}

        protected override void OnInputChanged(KeyboardInputButton oldValue, KeyboardInputButton newValue) {
            if (oldValue != null) {
                oldValue.Used--;
            }

            if (newValue != null) {
                newValue.Used++;
            }
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt("KEY", -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.SetCommentary("KEY", Input?.DisplayName);
            section.Set("KEY", Input == null ? @"-1" : @"0x" + Input?.Id.ToString(@"X"));
        }
    }

    public class KeyboardSpecificButtonEntry : KeyboardButtonEntry {
        public KeyboardSpecificButtonEntry([LocalizationRequired(false)] string id, string name) : base(id, name) { }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini["KEYBOARD"];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt(Id, -1));
        }

        public override void Save(IniFile ini) {
            var section = ini["KEYBOARD"];
            section.SetCommentary("KEY", Input?.DisplayName);
            section.Set(Id, Input == null ? @"-1" : @"0x" + Input?.Id.ToString(@"X"));
        }
    }
}