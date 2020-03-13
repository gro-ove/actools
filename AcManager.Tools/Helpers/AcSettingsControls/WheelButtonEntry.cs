using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class WheelButtonEntry : BaseEntry<DirectInputButton>, IDirectInputEntry {
        private readonly bool _supportsPov;
        private readonly bool _modifier;

        public WheelButtonEntry(string id, string name, bool supportsPov = false, bool modifier = false) : base(id, name) {
            _supportsPov = supportsPov;
            _modifier = modifier;
        }

        public override bool IsCompatibleWith(DirectInputButton obj) {
            return !_modifier && (_supportsPov || PatchHelper.IsFeatureSupported(PatchHelper.FeaturePovForButtons))
                    ? base.IsCompatibleWith(obj)
                    : obj?.GetType() == typeof(DirectInputButton);
        }

        public override EntryLayer Layer {
            get {
                if (_modifier) {
                    // return EntryLayer.NoIntersection;
                }

                var modifierInput = ModifierButton?.Input;
                if (modifierInput != null) {
                    return modifierInput.Id + EntryLayer.ButtonModifier;
                }

                return base.Layer;
            }
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];

            var deviceId = section.GetInt(_modifier ? "JOY_MODIFICATOR" : "JOY", -1);
            var device = devices.FirstOrDefault(x => x.OriginalIniIds.Contains(deviceId));

            var pov = _modifier ? -1 : section.GetInt("__CM_POV", -1);
            var direction = section.GetIntEnum("__CM_POV_DIR", DirectInputPovDirection.Up);
            var input = pov != -1 ? device?.GetPov(pov, direction) : device?.GetButton(section.GetInt(_modifier ? "BUTTON_MODIFICATOR" : "BUTTON", -1));
            Input = input;
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            section.Set(_modifier ? "JOY_MODIFICATOR" : "JOY", Input?.Device.Index);

            if (!_modifier && Input is DirectInputPov pov) {
                section.Set(@"BUTTON", -1);
                section.Set("__CM_POV", pov.Id);
                section.SetIntEnum("__CM_POV_DIR", pov.Direction);
            } else {
                section.Set(_modifier ? "BUTTON_MODIFICATOR" : "BUTTON", Input?.Id ?? -1);
                if (!_modifier) {
                    section.Remove(@"__CM_POV");
                    section.Remove(@"__CM_POV_DIR");
                }
            }
        }

        IDirectInputDevice IDirectInputEntry.Device => Input?.Device;

        [CanBeNull]
        public WheelButtonEntry ModifierButton { get; set; }
    }
}