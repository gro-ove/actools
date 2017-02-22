using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AcManager.Tools.Helpers.AcSettings;
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

        [DllImport(@"user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // FOR GOD’S SAKE KUNOS!
        private static bool CheckValue(int value) {
            try {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                Convert.ToChar(MapVirtualKey((uint)value, 2U));
                return true;
            } catch (Exception) {
                return false;
            }
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.SetCommentary("KEY", Input?.DisplayName);
            section.Set("KEY", Input == null || !CheckValue(Input.Id) ? @"-1" : @"0x" + Input.Id.ToString(@"X"));
        }
    }
}