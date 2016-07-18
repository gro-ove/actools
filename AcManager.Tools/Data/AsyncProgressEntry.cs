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
                return new AsyncProgressEntry(string.Format(Resources.Progress_Downloading, receivedBytes.ReadableSize(1)), null);
            }

            return new AsyncProgressEntry(string.Format(Resources.Progress_Downloading_KnownTotal, receivedBytes.ReadableSize(1), totalBytes.ReadableSize(1)),
                    (double)receivedBytes / totalBytes);
        }

        public static AsyncProgressEntry CreateUploading(long sentBytes, long totalBytes) {
            if (totalBytes == -1) {
                return new AsyncProgressEntry(string.Format(Resources.Progress_Uploading, sentBytes.ReadableSize(1)), null);
            }

            return new AsyncProgressEntry(string.Format(Resources.Progress_Uploading_KnownTotal, sentBytes.ReadableSize(1), totalBytes.ReadableSize(1)),
                    (double)sentBytes / totalBytes);
        }
    }
}