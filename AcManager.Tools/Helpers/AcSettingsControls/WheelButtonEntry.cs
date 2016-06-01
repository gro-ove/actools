using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public sealed class WheelButtonEntry : BaseEntry<DirectInputButton> {
        public WheelButtonEntry(string id, string name) : base(id, name) {}

        public override void Load(IniFile ini, IReadOnlyList<DirectInputDevice> devices) {
            var section = ini[Id];
            Input = devices.ElementAtOrDefault(section.GetInt("JOY", -1))?
                           .Buttons.ElementAtOrDefault(section.GetInt("BUTTON", -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set("JOY", Input?.Device.IniId ?? -1);
            section.Set("BUTTON", Input?.Id ?? -1);
        }
    }
}