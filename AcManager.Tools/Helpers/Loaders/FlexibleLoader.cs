using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public static class FlexibleLoader {
        [CanBeNull]
        public static ICmRequestHandler CmRequestHandler { get; set; }

        private static readonly List<ILoaderFactory> Factories = new List<ILoaderFactory>();

        public static void Register([NotNull] ILoaderFactory factory) {
            Factories.Add(factory);
        }

        [NotNull]
        public static ILoader CreateLoader([NotNull] string url) {
            if (CmRequestHandler?.Test(url) == true) {
                var unwrapped = CmRequestHandler.UnwrapDownloadUrl(url);
                if (unwrapped != null) {
                    url = unwrapped;
                    Logging.Debug($"Unwrapped URL: {unwrapped}");
                } else {
                    throw new OperationCanceledException("Link is handled by CM Requests Handler");
                }
            }

            var loader = Factories.Select(x => x.Create(url)).FirstOrDefault(x => x != null);
            if (loader != null) {
                return loader;
            }

            if (AcStuffSharedLoader.Test(url)) return new AcStuffSharedLoader(url);
            if (GoogleDriveLoader.Test(url)) return new GoogleDriveLoader(url);
            if (YandexDiskLoader.Test(url)) return new YandexDiskLoader(url);
            if (MediaFireLoader.Test(url)) return new MediaFireLoader(url);
            if (DropboxLoader.Test(url)) return new DropboxLoader(url);
            if (OneDriveLoader.Test(url)) return new OneDriveLoader(url);
            if (AcClubLoader.Test(url)) return new AcClubLoader(url);
            if (AcDriftingProLoader.Test(url)) return new AcDriftingProLoader(url);
            if (AssettoDbLoader.Test(url)) return new AssettoDbLoader(url);
            if (AdFlyLoader.Test(url)) return new AdFlyLoader(url);
            if (MegaLoader.Test(url)) return new MegaLoader(url);
            return new DirectLoader(url);
        }

        private static IWebProxy _proxy;

        public static void SetProxy(IWebProxy proxy) {
            _proxy = proxy;
        }

        public static async Task<string> UnwrapLink(string argument, CancellationToken cancellation = default(CancellationToken)) {
            var loader = CreateLoader(argument);
            using (var order = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromMinutes(10))) {
                var client = order.Victim;

                if (_proxy != null) {
                    client.Proxy = _proxy;
                }

                if (!await loader.PrepareAsync(client, cancellation)) {
                    throw new InformativeException("Can’t load file", "Loader preparation failed.");
                }

                return await loader.GetDownloadLink(cancellation);
            }
        }

        public static async Task<string> LoadAsyncTo(string argument,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination, [CanBeNull] FlexibleLoaderReportDestinationCallback reportDestination,
                Action<FlexibleLoaderMetaInformation> reportMetaInformation = null, Func<bool> checkIfPaused = null,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            var loader = CreateLoader(argument);
            try {
                using (var order = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromMinutes(10))) {
                    var client = order.Victim;

                    if (_proxy != null) {
                        client.Proxy = _proxy;
                    }

                    progress?.Report(AsyncProgressEntry.Indetermitate);

                    cancellation.ThrowIfCancellationRequested();
                    cancellation.Register(client.CancelAsync);

                    if (!await loader.PrepareAsync(client, cancellation)) {
                        throw new InformativeException("Can’t load file", "Loader preparation failed.");
                    }

                    cancellation.ThrowIfCancellationRequested();
                    reportMetaInformation?.Invoke(FlexibleLoaderMetaInformation.FromLoader(loader));

                    var initialProgressCallback = true;
                    var reportStopwatch = Stopwatch.StartNew();
                    var progressStopwatch = new AsyncProgressBytesStopwatch();

                    if (loader.UsesClientToDownload) {
                        client.DownloadProgressChanged += (sender, args) => {
                            if (initialProgressCallback) {
                                reportMetaInformation?.Invoke(FlexibleLoaderMetaInformation.FromLoader(loader));
                                initialProgressCallback = false;
                            }

                            if (reportStopwatch.Elapsed.TotalMilliseconds < 20) return;
                            order.Delay();
                            reportStopwatch.Restart();
                            progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, args.TotalBytesToReceive == -1
                                    && loader.TotalSize.HasValue ? Math.Max(loader.TotalSize.Value, args.BytesReceived) : args.TotalBytesToReceive, progressStopwatch));
                        };
                    }

                    var loaded = await loader.DownloadAsync(client, getPreferredDestination, reportDestination, checkIfPaused,
                            loader.UsesClientToDownload ? null : new Progress<long>(p => {
                                if (initialProgressCallback) {
                                    reportMetaInformation?.Invoke(FlexibleLoaderMetaInformation.FromLoader(loader));
                                    initialProgressCallback = false;
                                }

                                if (reportStopwatch.Elapsed.TotalMilliseconds < 20) return;
                                order.Delay();
                                reportStopwatch.Restart();
                                progress?.Report(loader.TotalSize.HasValue ? AsyncProgressEntry.CreateDownloading(p, loader.TotalSize.Value, progressStopwatch)
                                        : new AsyncProgressEntry(string.Format(UiStrings.Progress_Downloading, p.ToReadableSize(1)), null));
                            }), cancellation);

                    cancellation.ThrowIfCancellationRequested();
                    Logging.Write("Loaded: " + loaded);
                    return loaded;
                }
            } catch (Exception e) when (cancellation.IsCancellationRequested || e.IsCancelled()) {
                Logging.Warning("Cancelled");
                throw new OperationCanceledException();
            } catch (Exception e) {
                Logging.Warning(e);
                throw;
            }
        }
    }
}