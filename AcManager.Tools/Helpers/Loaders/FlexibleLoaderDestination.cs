using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public class FlexibleLoaderDestination {
        public FlexibleLoaderDestination([NotNull] string filename, bool canResumeDownload) {
            Filename = filename;
            CanResumeDownload = canResumeDownload;
        }

        [NotNull]
        public string Filename { get; }
        public bool CanResumeDownload { get; }
    }
}