using AcManager.Tools.Helpers;
using CefSharp;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.UserControls.CefSharp {
    internal class DownloadHandler : IDownloadHandler {
        private WaitingDialog _waiting;
        private AsyncProgressBytesStopwatch _stopwatch;
        private string _filename;

        // TODO: Several files at once!

        public void OnBeforeDownload(IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
            Logging.Debug(downloadItem.SuggestedFileName);
            var downloadPath = @"U:\test\";
            if (!callback.IsDisposed) {
                using (callback) {
                    _filename = downloadPath + downloadItem.SuggestedFileName;
                    callback.Continue(_filename, false);
                }
            }

            ActionExtension.InvokeInMainThread(() => {
                _waiting?.Dispose();
                _waiting = WaitingDialog.Create(downloadItem.SuggestedFileName);
                _stopwatch = new AsyncProgressBytesStopwatch();
            });
        }

        public void OnDownloadUpdated(IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
            ActionExtension.InvokeInMainThread(() => {
                if (downloadItem.IsInProgress) {
                    _waiting?.Report(AsyncProgressEntry.CreateDownloading(downloadItem.ReceivedBytes, downloadItem.TotalBytes, _stopwatch));
                } else {
                    WindowsHelper.ViewFile(_filename);
                    _waiting?.Dispose();
                }
            });
        }
    }
}