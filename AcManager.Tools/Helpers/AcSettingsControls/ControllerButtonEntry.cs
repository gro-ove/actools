using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class ControllerButtonEntry : BaseEntry<DirectInputButton>, IDirectInputEntry {
        public ControllerButtonEntry(string id, string name) : base(id, name) { }

        private static readonly Dictionary<DirectInputPovDirection, string> PovIds = new Dictionary<DirectInputPovDirection, string> {
            [DirectInputPovDirection.Up] = @"DPAD_UP",
            [DirectInputPovDirection.Down] = @"DPAD_DOWN",
            [DirectInputPovDirection.Left] = @"DPAD_LEFT",
            [DirectInputPovDirection.Right] = @"DPAD_RIGHT",
        };

        private static readonly string[] KeyIds = {
            @"A", @"B", @"X", @"Y", @"LSHOULDER", @"RSHOULDER", @"BACK", @"START", @"LTHUMB_PRESS", @"RTHUMB_PRESS"
        };

        public override bool IsCompatibleWith(DirectInputButton obj) {
            return obj?.Device.IsController == true && (obj.Id >= 0 && obj.Id < KeyIds.Length || obj.GetType() == typeof(DirectInputPov));
        }

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var device = devices.FirstOrDefault(x => x.IsController);
            var value = ini[Id].GetNonEmpty("XBOXBUTTON");
            Input = value?.StartsWith(@"DPAD_") == true
                    ? device?.GetPov(0, PovIds.FirstOrDefault(x => x.Value == value).Key)
                    : device?.GetButton(KeyIds.IndexOf(value));
        }

        public override void Save(IniFile ini) {
            ini[Id].Set(@"XBOXBUTTON", Input is DirectInputPov pov ? PovIds.GetValueOrDefault(pov.Direction) : KeyIds.ElementAtOrDefault(Input?.Id ?? -1));
        }

        IDirectInputDevice IDirectInputEntry.Device => Input?.Device;
    }
}