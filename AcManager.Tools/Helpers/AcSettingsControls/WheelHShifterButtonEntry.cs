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

            var deviceId = section.GetInt("JOY", -1);
            var device = devices.FirstOrDefault(x => x.OriginalIniIds.Contains(deviceId));
            Input = device?.GetButton(section.GetInt(Id, -1));
        }

        public override void Save(IniFile ini) {
            var section = ini["SHIFTER"];
            section.Set("JOY", Input?.Device.Index);
            section.Set(Id, Input?.Id ?? -1);
        }
    }
}