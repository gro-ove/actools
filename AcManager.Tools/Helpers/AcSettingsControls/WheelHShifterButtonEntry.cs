using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public sealed class WheelHShifterButtonEntry : WheelButtonEntry {
        public WheelHShifterButtonEntry([LocalizationRequired(false)] string id, string name, string shortName) : base(id, name) {
            ShortName = shortName;
        }

        public string ShortName { get; }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini["SHIFTER"];
            Input = devices.ElementAtOrDefault(section.GetInt("JOY", -1))?.GetButton(section.GetInt(Id, -1));
        }

        public override void Save(IniFile ini) {
            var section = ini["SHIFTER"];
            section.Set("JOY", Input?.Device.IniId ?? -1);
            section.Set(Id, Input?.Id ?? -1);
        }
    }
}