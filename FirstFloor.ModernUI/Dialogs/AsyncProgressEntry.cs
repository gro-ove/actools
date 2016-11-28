using System.ComponentModel;
using System.Runtime.CompilerServices;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Dialogs {
    public struct AsyncProgressEntry : INotifyPropertyChanged {
        public string Message { get; }

        public double? Progress { get; }

        public static readonly AsyncProgressEntry Indetermitate = new AsyncProgressEntry("", 0d);
        public static readonly AsyncProgressEntry Ready = new AsyncProgressEntry("", 1d);

        public static AsyncProgressEntry FromStringIndetermitate(string message) {
            return new AsyncProgressEntry(message, 0d);
        }

        public bool IsIndeterminate => Message == "" && Equals(Progress, 0d);

        public bool IsReady => Equals(Progress, 1d);

        public AsyncProgressEntry(string message, double? progress) {
            Message = message;
            Progress = progress;
        }

        public AsyncProgressEntry(string message, int value, int total) {
            Message = message;
            Progress = (double)value / total + 0.000001;
        }

        public static AsyncProgressEntry CreateDownloading(long receivedBytes, long totalBytes) {
            return totalBytes == -1
                    ? new AsyncProgressEntry(string.Format(UiStrings.Progress_Downloading, receivedBytes.ToReadableSize(1)), null)
                    : new AsyncProgressEntry(
                            string.Format(UiStrings.Progress_Downloading_KnownTotal, receivedBytes.ToReadableSize(1), totalBytes.ToReadableSize(1)),
                            (double)receivedBytes / totalBytes);
        }

        public static AsyncProgressEntry CreateUploading(long sentBytes, long totalBytes) {
            return totalBytes == -1
                    ? new AsyncProgressEntry(string.Format(UiStrings.Progress_Uploading, sentBytes.ToReadableSize(1)), null)
                    : new AsyncProgressEntry(string.Format(UiStrings.Progress_Uploading_KnownTotal, sentBytes.ToReadableSize(1), totalBytes.ToReadableSize(1)),
                            (double)sentBytes / totalBytes);
        }

        public override string ToString() {
            return $@"{(Message ?? @"<NULL>")} ({Progress*100:F1}%)";
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
            add { }
            remove { }
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {}
    }
}