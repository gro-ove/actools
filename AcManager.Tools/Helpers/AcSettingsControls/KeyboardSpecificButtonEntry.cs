using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
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