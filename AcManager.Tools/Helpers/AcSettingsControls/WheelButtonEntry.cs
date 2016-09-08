using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class WheelButtonEntry : BaseEntry<DirectInputButton>, IDirectInputEntry {
        public WheelButtonEntry(string id, string name) : base(id, name) {}

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];

            var deviceId = section.GetInt("JOY", -1);
            var device = devices.FirstOrDefault(x => x.OriginalIniIds.Contains(deviceId));
            Input = device?.GetButton(section.GetInt("BUTTON", -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set("JOY", Input?.Device.Index);
            section.Set("BUTTON", Input?.Id ?? -1);
        }

        IDirectInputDevice IDirectInputEntry.Device => Input?.Device;
    }
}