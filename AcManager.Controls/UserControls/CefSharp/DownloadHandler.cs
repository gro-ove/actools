using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using CefSharp;
using CefSharp.Wpf;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.CefSharp {
    internal class DownloadHandler : IDownloadHandler {
        private class Registered {
            public readonly WeakReference<ChromiumWebBrowser> Browser;

            [CanBeNull]
            public readonly string[] AllowedHosts;

            public readonly IWebDownloadListener Listener;

            public Registered(WeakReference<ChromiumWebBrowser> browser, string[] allowedHosts, IWebDownloadListener listener) {
                Browser = browser;
                AllowedHosts = allowedHosts;
                Listener = listener;
            }
        }

        private readonly List<Registered> _registered = new List<Registered>(5);

        public void Register([NotNull] ChromiumWebBrowser browser, [CanBeNull] string[] allowedHosts, IWebDownloadListener callback) {
            if (browser == null) throw new ArgumentNullException(nameof(browser));
            _registered.RemoveAll(x => !x.Browser.TryGetTarget(out var b) || ReferenceEquals(b, browser));
            if (allowedHosts?.Length > 0) {
                var hosts = allowedHosts.ArrayContains(@"*") ? null : allowedHosts.Select(x => x.GetDomainNameFromUrl()).Distinct().ToArray();
                _registered.Add(new Registered(new WeakReference<ChromiumWebBrowser>(browser), hosts, callback));
            }
        }

        private class DownloadData {
            internal TaskCompletionSource<string> TaskCompletionSource;
            internal string Destination;
            internal IProgress<long> Progress;
            internal CancellationToken CancellationToken;
        }

        private class WebDownloader : IWebDownloader {
            internal DownloadData Data;

            public Task<string> Download(string destination, IProgress<long> progress, CancellationToken cancellation) {
                var tcs = new TaskCompletionSource<string>();
                Data = new DownloadData {
                    TaskCompletionSource = tcs,
                    Destination = destination,
                    Progress = progress,
                    CancellationToken = cancellation
                };
                return tcs.Task;
            }
        }

        private Dictionary<int, DownloadData> _downloads = new Dictionary<int, DownloadData>();

        void IDownloadHandler.OnBeforeDownload(IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
            for (var i = 0; i < _registered.Count; i++) {
                var registered = _registered[i];
                if (!registered.Browser.TryGetTarget(out var targetBrowser)) {
                    _registered.RemoveAt(i);
                    continue;
                }

                if (targetBrowser.GetBrowser().Identifier == browser.Identifier) {
                    var hostName = downloadItem.Url.GetDomainNameFromUrl();
                    if (registered.AllowedHosts?.Contains(hostName, StringComparer.OrdinalIgnoreCase) != false) {
                        try {
                            if (!callback.IsDisposed) {
                                using (callback) {
                                    var loader = new WebDownloader();
                                    registered.Listener.OnDownload(downloadItem.Url, downloadItem.SuggestedFileName, downloadItem.TotalBytes, loader);
                                    if (loader.Data != null) {
                                        callback.Continue(loader.Data.Destination, false);
                                        _downloads[downloadItem.Id] = loader.Data;
                                    }
                                }
                            }
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    }
                }
            }
        }

        void IDownloadHandler.OnDownloadUpdated(IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
            if (_downloads.TryGetValue(downloadItem.Id, out var data)) {
                try {
                    if (!downloadItem.IsValid) {
                        throw new Exception("Download item isn’t valid anymore");
                    }

                    if (downloadItem.IsCancelled) {
                        throw new OperationCanceledException();
                    }

                    if (data.CancellationToken.IsCancellationRequested) {
                        if (!callback.IsDisposed) {
                            using (callback) {
                                callback.Cancel();
                            }
                        }
                        throw new OperationCanceledException();
                    }

                    if (downloadItem.IsInProgress) {
                        data.Progress.Report(downloadItem.ReceivedBytes);
                    } else {
                        _downloads.Remove(downloadItem.Id);
                        data.TaskCompletionSource.TrySetResult(downloadItem.FullPath);
                    }
                } catch (Exception e) when (e.IsCancelled()) {
                    _downloads.Remove(downloadItem.Id);
                    data.TaskCompletionSource.TrySetCanceled();
                } catch (Exception e) {
                    _downloads.Remove(downloadItem.Id);
                    data.TaskCompletionSource.TrySetException(e);
                }
            }
        }
    }
}