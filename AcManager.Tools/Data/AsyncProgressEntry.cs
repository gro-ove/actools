using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Data {
    public struct AsyncProgressEntry {
        public string Message;
        public double? Progress;

        public static readonly AsyncProgressEntry Indetermitate = new AsyncProgressEntry("", 0d);

        public AsyncProgressEntry(string message, double? progress) {
            Message = message;
            Progress = progress;
        }

        public static AsyncProgressEntry CreateDownloading(long receivedBytes, long totalBytes) {
            if (totalBytes == -1) {
                return new AsyncProgressEntry($@"Loaded {LocalizationHelper.ReadableSize(receivedBytes, 1)}", null);
            }

            return new AsyncProgressEntry($@"Loaded {LocalizationHelper.ReadableSize(receivedBytes, 1)} of {LocalizationHelper.ReadableSize(totalBytes, 1)}",
                    (double)receivedBytes / totalBytes);
        }
    }
}