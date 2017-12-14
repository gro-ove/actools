using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public class FlexibleLoaderMetaInformation {
        public FlexibleLoaderMetaInformation(long? totalSize, [CanBeNull] string fileName, [CanBeNull] string version) {
            TotalSize = totalSize;
            FileName = fileName;
            Version = version;
        }

        public long? TotalSize { get; set; }

        [CanBeNull]
        public string FileName { get; set; }

        [CanBeNull]
        public string Version { get; set; }

        public string GetFootprint() {
            return $"filename={FileName}, size={TotalSize}".ToCutBase64();
        }

        public static FlexibleLoaderMetaInformation FromLoader(ILoader loader) {
            return new FlexibleLoaderMetaInformation(loader.TotalSize, loader.FileName, loader.Version);
        }
    }
}