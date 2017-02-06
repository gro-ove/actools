using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcLog {
    public interface IAcLogHelperExtension {
        [CanBeNull]
        WhatsGoingOn Detect([NotNull] string log, [CanBeNull] string crash);
    }
}