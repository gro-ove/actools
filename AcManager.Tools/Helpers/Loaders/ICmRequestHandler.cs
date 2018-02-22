using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ICmRequestHandler {
        bool Test([NotNull] string request);

        [CanBeNull]
        string UnwrapDownloadUrl([NotNull] string request);

        void Handle([NotNull] string request);
    }
}