using System;
using System.Threading.Tasks;
using AcManager.Tools.WorkshopPublishTools.Submitters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Workshop {
    public class UploadLogger : IUploadLogger {
        private readonly BetterObservableCollection<string> _destination;
        private int _level;
        private int _progressDotsLeft;
        private bool _progressDotsRunning;

        public UploadLogger([NotNull] BetterObservableCollection<string> destination) {
            _destination = destination;
            _destination.Clear();
        }

        private class UploadLoggedOperation : IUploadLoggedOperation {
            private readonly UploadLogger _logger;
            private string _message;

            public UploadLoggedOperation(UploadLogger logger) {
                _logger = logger;
                _message = "done.";
            }

            public void Dispose() {
                _logger.End(_message);
            }

            public void SetResult(string message) {
                _message = message;
            }

            public void SetFailed() {
                _message = "failed.";
            }
        }

        public IUploadLoggedOperation Begin(string message) {
            CollapseProgressDots();
            InnerWrite(message, null, _level);
            ++_level;
            _progressDotsLeft = 3;
            ProgressDots();
            return new UploadLoggedOperation(this);
        }

        private class UploadLoggedParallelOperation : IUploadLoggedParallelOperation {
            private readonly Action<string> _callback;
            private string _message;
            private string _trimStart;
            private DateTime _lastReport;

            public UploadLoggedParallelOperation(Action<string> callback, string trimStart) {
                _callback = callback;
                _trimStart = trimStart;
                _message = "done.";
            }

            public void Dispose() {
                _callback.Invoke(_message);
            }

            public void SetResult(string message) {
                _message = message;
            }

            public void SetFailed() {
                _message = "failed.";
            }

            public void Report(AsyncProgressEntry value) {
                var now = DateTime.Now;
                if ((now - _lastReport).TotalMilliseconds < 20d) return;
                _lastReport = now;
                ActionExtension.InvokeInMainThreadAsync(() => { _callback.Invoke(TrimProgressMessage(value, _trimStart)); });
            }
        }

        public IUploadLoggedParallelOperation BeginParallel(string message, string trimStart = null) {
            CollapseProgressDots();
            InnerWrite(message, null, _level);
            var index = _destination.Count - 1;
            string messageBase = null;
            var progressDotsLeft = 3;
            LocalProgressDots();

            return new UploadLoggedParallelOperation(m => {
                if (progressDotsLeft > 0) {
                    _destination[index] += @".".RepeatString(progressDotsLeft);
                    progressDotsLeft = 0;
                }
                if (messageBase == null) {
                    messageBase = _destination[index] + @" ";
                }
                _destination[index] = messageBase + m;
            }, trimStart);

            async void LocalProgressDots() {
                while (progressDotsLeft > 0) {
                    await Task.Delay(500);
                    if (progressDotsLeft == 0) break;
                    _destination[index] += @".";
                    --progressDotsLeft;
                }
            }
        }

        internal void End(string message) {
            CollapseProgressDots();
            if (_level == 0) throw new Exception("Unexpected end");
            InnerWrite(message, null, --_level, true);
        }

        public void Write(string message) {
            CollapseProgressDots();
            InnerWrite(message, null, _level);
        }

        public void Error(string message) {
            CollapseProgressDots();
            InnerWrite(message, @"ff0000", _level);
        }

        private void CollapseProgressDots() {
            if (_progressDotsLeft > 0) {
                _destination[_destination.Count - 1] += @".".RepeatString(_progressDotsLeft);
                _progressDotsLeft = 0;
            }
        }

        private async void ProgressDots() {
            if (_progressDotsRunning) return;
            _progressDotsRunning = true;
            while (_progressDotsLeft > 0) {
                await Task.Delay(500);
                if (_progressDotsLeft == 0) break;
                _destination[_destination.Count - 1] += @".";
                --_progressDotsLeft;
            }
            _progressDotsRunning = false;
        }

        private void InnerWrite(string message, string color = null, int level = 0, bool extraHalf = false) {
            _destination.Add($@"{@"  ".RepeatString(level * 2 + (extraHalf ? 1 : 0))}{
                    (color != null ? $@"[color=#{color}]" : "")}{BbCodeBlock.Encode(message)}{(color != null ? @"[/color]" : "")}");
        }

        private static string TrimProgressMessage(AsyncProgressEntry value, string trimStart) {
            var msg = value.Message?.ToSentenceMember() ?? "";
            if (trimStart != null && msg.Length > trimStart.Length
                    && string.Equals(msg.Substring(0, trimStart.Length), trimStart, StringComparison.OrdinalIgnoreCase)) {
                msg = msg.Substring(trimStart.Length).TrimStart();
            }
            return msg;
        }
    }
}