using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public class FlexibleLoaderMetaInformation {
        public FlexibleLoaderMetaInformation() { }

        public FlexibleLoaderMetaInformation(long? totalSize, [CanBeNull] string fileName, [CanBeNull] string version, bool canPause) {
            TotalSize = totalSize;
            FileName = fileName;
            Version = version;
            CanPause = canPause;
        }

        public long? TotalSize { get; set; }

        [CanBeNull]
        public string FileName { get; set; }

        [CanBeNull]
        public string Version { get; set; }

        public bool CanPause { get; set; }

        public static FlexibleLoaderMetaInformation FromLoader(ILoader loader) {
            return new FlexibleLoaderMetaInformation(loader.TotalSize, loader.FileName, loader.Version, loader.CanPause);
        }
    }
}