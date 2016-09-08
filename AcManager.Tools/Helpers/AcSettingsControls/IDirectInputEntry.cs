using AcManager.Tools.Helpers.DirectInput;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public interface IDirectInputEntry : IEntry {
        [CanBeNull]
        IDirectInputDevice Device { get; }
    }
}