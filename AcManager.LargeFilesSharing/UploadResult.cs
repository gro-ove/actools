using AcManager.Internal;

namespace AcManager.LargeFilesSharing {
    public class UploadResult : InternalUtils.ShareResult {
        public string DirectUrl { get; internal set; }
    }
}