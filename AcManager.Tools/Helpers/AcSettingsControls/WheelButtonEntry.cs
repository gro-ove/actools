using AcManager.Tools.Helpers.DirectInput;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public sealed class WheelButtonEntry : BaseEntry<DirectInputButton> {
        public WheelButtonEntry(string id, string name) : base(id, name) {}
    }
}