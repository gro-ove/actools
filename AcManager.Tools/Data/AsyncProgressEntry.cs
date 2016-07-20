using System;
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

        public AsyncProgressEntry(string message, int value, int total) {
            Message = message;
            Progress = (double)value / total + 0.000001;
        }

        public static AsyncProgressEntry CreateDownloading(long receivedBytes, long totalBytes) {
            if (totalBytes == -1) {
                return new AsyncProgressEntry(string.Format(Resources.Progress_Downloading, receivedBytes.ToReadableSize(1)), null);
            }

            return new AsyncProgressEntry(string.Format(Resources.Progress_Downloading_KnownTotal, receivedBytes.ToReadableSize(1), totalBytes.ToReadableSize(1)),
                    (double)receivedBytes / totalBytes);
        }

        public static AsyncProgressEntry CreateUploading(long sentBytes, long totalBytes) {
            if (totalBytes == -1) {
                return new AsyncProgressEntry(string.Format(Resources.Progress_Uploading, sentBytes.ToReadableSize(1)), null);
            }

            return new AsyncProgressEntry(string.Format(Resources.Progress_Uploading_KnownTotal, sentBytes.ToReadableSize(1), totalBytes.ToReadableSize(1)),
                    (double)sentBytes / totalBytes);
        }
    }
}