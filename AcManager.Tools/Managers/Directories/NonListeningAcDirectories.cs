using AcManager.Tools.Managers.InnerHelpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Directories {
    /// <summary>
    /// This thing doesn’t subscribe or listen to anything.
    /// </summary>
    public class NonListeningAcDirectories : BaseAcDirectories {
        public NonListeningAcDirectories([NotNull] string enabledDirectory, [CanBeNull] string disabledDirectory)
                : base(enabledDirectory, disabledDirectory) { }

        public NonListeningAcDirectories([NotNull] string enabledDirectory)
                : base(enabledDirectory) { }

        public override void Subscribe(IDirectoryListener listener) {
        }

        public override void Dispose() {
        }
    }
}