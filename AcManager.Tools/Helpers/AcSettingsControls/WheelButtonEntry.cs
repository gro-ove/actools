using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class WheelButtonEntry : BaseEntry<DirectInputButton>, IDirectInputEntry {
        private readonly bool _supportsPov;

        public WheelButtonEntry(string id, string name, bool supportsPov = false) : base(id, name) {
            _supportsPov = supportsPov;
        }

        public override bool IsCompatibleWith(DirectInputButton obj) {
            return _supportsPov || PatchHelper.IsFeatureSupported(PatchHelper.FeaturePovForButtons)
                    ? base.IsCompatibleWith(obj)
                    : obj?.GetType() == typeof(DirectInputButton);
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];

            var deviceId = section.GetInt("JOY", -1);
            var device = devices.FirstOrDefault(x => x.OriginalIniIds.Contains(deviceId));

            var pov = section.GetInt("__CM_POV", -1);
            var direction = section.GetIntEnum("__CM_POV_DIR", DirectInputPovDirection.Up);
            var input = pov != -1 ? device?.GetPov(pov, direction) : device?.GetButton(section.GetInt("BUTTON", -1));
            Input = input;
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set("JOY", Input?.Device.Index);

            if (Input is DirectInputPov pov) {
                section.Set(@"BUTTON", -1);
                section.Set("__CM_POV", pov.Id);
                section.SetIntEnum("__CM_POV_DIR", pov.Direction);
            } else {
                section.Set("BUTTON", Input?.Id ?? -1);
                section.Remove(@"__CM_POV");
                section.Remove(@"__CM_POV_DIR");
            }
        }

        IDirectInputDevice IDirectInputEntry.Device => Input?.Device;
    }
}