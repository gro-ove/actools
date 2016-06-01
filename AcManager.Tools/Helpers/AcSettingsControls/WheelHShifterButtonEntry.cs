using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public sealed class WheelHShifterButtonEntry : BaseEntry<DirectInputButton> {
        public WheelHShifterButtonEntry(string id, string name, string shortName) : base(id, name) {
            ShortName = shortName;
        }

        public string ShortName { get; }

        public override void Load(IniFile ini, IReadOnlyList<DirectInputDevice> devices) {
            var section = ini["SHIFTER"];
            Input = devices.ElementAtOrDefault(section.GetInt("JOY", -1))?
                           .Buttons.ElementAtOrDefault(section.GetInt(Id, -1));
        }

        public override void Save(IniFile ini) {
            var section = ini["SHIFTER"];
            section.Set("JOY", Input?.Device.IniId ?? -1);
            section.Set(Id, Input?.Id ?? -1);
        }
    }
}