using System.Collections.Generic;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class KeyboardSpecificButtonEntry : KeyboardButtonEntry {
        private readonly string _section;

        public KeyboardSpecificButtonEntry([LocalizationRequired(false)] string id, string name, string section = "KEYBOARD") : base(id, name) {
            _section = section;
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[_section];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt(Id, -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[_section];
            section.SetCommentary(Id, Input?.DisplayName);
            section.Set(Id, Input == null ? @"-1" : @"0x" + Input?.Id.ToString(@"X"));
        }
    }
}