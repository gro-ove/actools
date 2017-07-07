using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    public class InstallationDetails {
        [NotNull]
        public CopyCallback CopyCallback;

        [NotNull]
        public string[] ToRemoval;

        public InstallationDetails([NotNull] CopyCallback copyCallback, [CanBeNull] string[] toRemoval) {
            CopyCallback = copyCallback;
            ToRemoval = toRemoval ?? new string[0];
        }
    }
}