using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Tools {
    /// <summary>
    /// In my best traditions: tricks and duck tape. To pass a download to ContentInstallationManager, this
    /// thing temporarily registers a new ILoaderFactory set to catch a single URL, immediately sends a new
    /// installation request, removes that ILoaderFactory and then connects together DownloadAsync() from
    /// ILoader and DownloadAsync() from IWebDownloader.
    /// </summary>
    public class WebDownloadListener : IWebDownloadListener {
        private class TemporaryFactoryAndLoader : ILoaderFactory, ILoader {
            private readonly string _url;
            private bool _triggered;

            public TemporaryFactoryAndLoader(string url) {
                _url = url;
            }

            Task<bool> ILoaderFactory.TestAsync(string url, CancellationToken cancellation) {
                if (_triggered) return Task.FromResult(false);
                _triggered |= _url == url;
                return Task.FromResult(_url == url);
            }

            Task<ILoader> ILoaderFactory.CreateAsync(string url, CancellationToken cancellation) {
                return Task.FromResult<ILoader>(this);
            }

            ILoader ILoader.Parent { get; set; }

            public long? TotalSize { get; private set; }
            public string FileName { get; private set; }

            string ILoader.Version => null;
            bool ILoader.UsesClientToDownload => false;
            bool ILoader.CanPause => false;

            Task<bool> ILoader.PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
                return Task.FromResult(true);
            }

            Task<string> ILoader.GetDownloadLink(CancellationToken cancellation) {
                return Task.FromResult(_url);
            }

            private TaskCompletionSource<string> _resultTask;
            private FlexibleLoaderGetPreferredDestinationCallback _destinationCallback;
            private FlexibleLoaderReportDestinationCallback _reportDestinationCallback;
            private IProgress<long> _progress;
            private CancellationToken _cancellation;

            Task<string> ILoader.DownloadAsync(CookieAwareWebClient client, FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                    FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused, IProgress<long> progress, CancellationToken cancellation) {
                _resultTask = new TaskCompletionSource<string>();
                _destinationCallback = getPreferredDestination;
                _reportDestinationCallback = reportDestination;
                _progress = progress;
                _cancellation = cancellation;
                return _resultTask.Task;
            }

            public async Task RunAsync(string suggestedName, long totalSize, IWebDownloader downloader) {
                try {
                    FlexibleLoader.RegisterPriority(this);
                    ContentInstallationManager.Instance.InstallAsync(_url, AddInstallMode.ForceNewTask,
                            ContentInstallationParams.DefaultWithExecutables).Ignore();
                    FlexibleLoader.Unregister(this);

                    var destinationCallback = _destinationCallback;
                    var reportDestinationCallback = _reportDestinationCallback;
                    var progress = _progress;
                    var resultTask = _resultTask;
                    if (destinationCallback == null) return;

                    TotalSize = totalSize;
                    FileName = suggestedName;

                    _destinationCallback = null;
                    _reportDestinationCallback = null;
                    _progress = null;

                    var destination = destinationCallback(_url, new FlexibleLoaderMetaInformation {
                        CanPause = false,
                        FileName = suggestedName,
                        TotalSize = totalSize
                    });

                    reportDestinationCallback?.Invoke(destination.Filename);

                    try {
                        if (SettingsHolder.WebBlocks.NotifyOnWebDownloads) {
                            Toast.Show("New download started", suggestedName ?? _url);
                        }

                        resultTask.TrySetResult(await downloader.DownloadAsync(destination.Filename, progress, _cancellation));
                    } catch (Exception e) {
                        Logging.Warning(e);
                        resultTask.TrySetException(e);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }
        }

        public void OnDownload(string url, string suggestedName, long totalSize, IWebDownloader downloader) {
            Logging.Write(url);
            ActionExtension.InvokeInMainThread(() => {
                new TemporaryFactoryAndLoader(url).RunAsync(suggestedName, totalSize, downloader).Ignore();
            });
        }
    }
}