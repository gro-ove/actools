using System;
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
        public static readonly AsyncProgressEntry Finished = new AsyncProgressEntry(null, null);

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

            const double x = 0.000001;
            Progress = (double)value / total * (1d - 2d * x) + x;
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
            return $@"{Message ?? @"<NULL>"} ({Progress*100:F1}%)";
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
            add { }
            remove { }
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {}

        public static IProgress<AsyncProgressEntry> Split(ref IProgress<AsyncProgressEntry> progress, double splitPoint) {
            var result = progress.Subrange(0.0001, splitPoint - 0.0001);
            progress = progress.Subrange(splitPoint + 0.0001, 0.9999);
            return result;
        }

        public static IProgress<AsyncProgressEntry> Split(ref IProgress<AsyncProgressEntry> progress, double splitPoint, string forceMessage,
                bool ignoreIndeterminate = true) {
            var result = progress.Subrange(0.0001, splitPoint - 0.0001, forceMessage, ignoreIndeterminate);
            progress = progress.Subrange(splitPoint + 0.0001, 0.9999);
            return result;
        }
    }

    public static class AsyncProgressEntryExtension {
        private class SubrangeProgress : IProgress<AsyncProgressEntry> {
            private readonly IProgress<AsyncProgressEntry> _baseProgress;
            private readonly double _from;
            private readonly double _range;
            private readonly string _forceMessage;
            private readonly bool _ignoreIndeterminate;

            public SubrangeProgress(IProgress<AsyncProgressEntry> baseProgress, double from, double range, string forceMessage = "",
                    bool ignoreIndeterminate = true) {
                _baseProgress = baseProgress;
                _from = from;
                _range = range;
                _forceMessage = forceMessage;
                _ignoreIndeterminate = ignoreIndeterminate;
            }

            public void Report(AsyncProgressEntry value) {
                if (_ignoreIndeterminate && value.IsIndeterminate) return;
                _baseProgress?.Report(new AsyncProgressEntry(
                        _forceMessage == "" ? value.Message : string.Format(_forceMessage, value.Message.ToSentenceMember()), _from + value.Progress * _range));
            }
        }

        [NotNull]
        public static IProgress<AsyncProgressEntry> Subrange([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, double from, double range,
                bool ignoreIndeterminate = true) {
            return new SubrangeProgress(baseProgress, from, range, ignoreIndeterminate: ignoreIndeterminate);
        }

        [NotNull]
        public static IProgress<double> ToDoubleProgress([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, string message) {
            return new Progress<double>(v => baseProgress.Report(message, v));
        }

        [NotNull]
        public static IProgress<AsyncProgressEntry> Subrange([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, double from, double range,
                string forceMessage, bool ignoreIndeterminate = true) {
            return new SubrangeProgress(baseProgress, from, range, forceMessage, ignoreIndeterminate);
        }

        public static void Report([CanBeNull] this IProgress<AsyncProgressEntry> progress, string message, double? value) {
            progress?.Report(new AsyncProgressEntry(message, value));
        }

        public static void Report([CanBeNull] this IProgress<AsyncProgressEntry> progress, string message, int i, int total) {
            progress?.Report(new AsyncProgressEntry(message, i, total));
        }

        private class SubrangeTupleProgress : IProgress<Tuple<string, double?>> {
            private readonly IProgress<Tuple<string, double?>> _baseProgress;
            private readonly double _from;
            private readonly double _range;
            private readonly string _forceMessage;
            private readonly bool _ignoreIndeterminate;

            public SubrangeTupleProgress(IProgress<Tuple<string, double?>> baseProgress, double from, double range, string forceMessage = "",
                    bool ignoreIndeterminate = true) {
                _baseProgress = baseProgress;
                _from = from;
                _range = range;
                _forceMessage = forceMessage;
                _ignoreIndeterminate = ignoreIndeterminate;
            }

            public void Report(Tuple<string, double?> value) {
                if (_ignoreIndeterminate && value.IsIndeterminate()) return;
                _baseProgress?.Report(new Tuple<string, double?>(
                        _forceMessage == "" ? value.Item1 : string.Format(_forceMessage, value.Item1.ToSentenceMember()), _from + value.Item2 * _range));
            }
        }

        public static bool IsIndeterminate([CanBeNull] this Tuple<string, double?> baseProgress) {
            return baseProgress?.Item1 == "" && Equals(baseProgress.Item2, 0d);
        }

        [NotNull]
        public static IProgress<Tuple<string, double?>> SubrangeTuple([CanBeNull] this IProgress<Tuple<string, double?>> baseProgress, double from, double range,
                bool ignoreIndeterminate = true) {
            return new SubrangeTupleProgress(baseProgress, from, range, ignoreIndeterminate: ignoreIndeterminate);
        }

        [NotNull]
        public static IProgress<Tuple<string, double?>> SubrangeTuple([CanBeNull] this IProgress<Tuple<string, double?>> baseProgress, double from, double range,
                string forceMessage, bool ignoreIndeterminate = true) {
            return new SubrangeTupleProgress(baseProgress, from, range, forceMessage, ignoreIndeterminate);
        }

        public static void ReportTuple([CanBeNull] this IProgress<Tuple<string, double?>> progress, string message, double? value) {
            progress?.Report(new Tuple<string, double?>(message, value));
        }

        public static void ReportTuple([CanBeNull] this IProgress<Tuple<string, double?>> progress, string message, int i, int total) {
            const double x = 0.000001;
            progress?.Report(new Tuple<string, double?>(message, (double)i / total * (1d - 2d * x) + x));
        }
    }
}