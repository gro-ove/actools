using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class WheelButtonAltEntry : WheelButtonEntry {
        private bool _isAvailable;

        public bool IsAvailable {
            get => _isAvailable;
            set => Apply(value, ref _isAvailable);
        }

        public WheelButtonAltEntry(string id, string name) : base(id, name, true) {}

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];

            var deviceId = section.GetInt("__CM_ALT_JOY", -1);
            var device = devices.FirstOrDefault(x => x.OriginalIniIds.Contains(deviceId));

            var pov = section.GetInt("__CM_ALT_POV", -1);
            var direction = section.GetIntEnum("__CM_ALT_POV_DIR", DirectInputPovDirection.Top);
            Input = pov != -1 ? device?.GetPov(pov, direction) : device?.GetButton(section.GetInt("__CM_ALT_BUTTON", -1));
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set("__CM_ALT_JOY", Input?.Device.Index);

            if (Input is DirectInputPov pov) {
                section.Set("__CM_ALT_POV", pov.Id);
                section.SetIntEnum("__CM_ALT_POV_DIR", pov.Direction);
            } else {
                section.Set("__CM_ALT_BUTTON", Input?.Id ?? -1);
                section.Remove(@"__CM_ALT_POV");
                section.Remove(@"__CM_ALT_POV_DIR");
            }
        }
    }
}