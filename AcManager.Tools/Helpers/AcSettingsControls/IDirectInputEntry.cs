using AcManager.Tools.Helpers.DirectInput;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public interface IDirectInputEntry {
        [CanBeNull]
        IDirectInputDevice Device { get; }
    }
}