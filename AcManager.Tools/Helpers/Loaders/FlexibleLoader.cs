using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
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

        public static void Unregister([NotNull] ILoaderFactory factory) {
            Factories.Remove(factory);
        }

        public static void Register([NotNull] ILoaderFactory factory) {
            Factories.Add(factory);
        }

        public static void RegisterPriority([NotNull] ILoaderFactory factory) {
            Factories.Insert(0, factory);
        }

        public static void Register<T>([NotNull] Func<string, bool> test) where T : ILoader {
            Factories.Add(new FnLoaderFactory<T>(test));
        }

        private class FnLoaderFactory<T> : ILoaderFactory where T : ILoader {
            private readonly Func<string, bool> _test;

            public FnLoaderFactory(Func<string, bool> test) {
                _test = test;
            }

            public Task<bool> TestAsync(string url, CancellationToken cancellation) {
                return Task.FromResult(_test(url));
            }

            public Task<ILoader> CreateAsync(string url, CancellationToken cancellation) {
                return Task.FromResult(_test(url) ? (ILoader)Activator.CreateInstance(typeof(T), url) : null);
            }
        }

        static FlexibleLoader() {
            Register<AcStuffSharedLoader>(AcStuffSharedLoader.Test);
            Register<GoogleDriveLoader>(GoogleDriveLoader.Test);
            Register<YandexDiskLoader>(YandexDiskLoader.Test);
            Register<MediaFireLoader>(MediaFireLoader.Test);
            Register<ShareModsLoader>(ShareModsLoader.Test);
            Register<DropboxLoader>(DropboxLoader.Test);
            Register<OneDriveLoader>(OneDriveLoader.Test);
            Register<AdFlyLoader>(AdFlyLoader.Test);
            Register<MegaLoader>(MegaLoader.Test);
            Register<LongenerLoader>(LongenerLoader.Test);
            Register<YouTubeDescriptionLoader>(YouTubeDescriptionLoader.Test);
        }

        public static bool IsSupportedFileStorage(string url) {
            return AcStuffSharedLoader.Test(url) ||
                    GoogleDriveLoader.Test(url) ||
                    YandexDiskLoader.Test(url) ||
                    MediaFireLoader.Test(url) ||
                    ShareModsLoader.Test(url) ||
                    DropboxLoader.Test(url) ||
                    OneDriveLoader.Test(url) ||
                    AdFlyLoader.Test(url) ||
                    MegaLoader.Test(url);
        }

        [ItemCanBeNull]
        public static async Task<ILoaderFactory> GetFactoryAsync(string url, CancellationToken cancellation) {
            try {
                foreach (var factory in Factories) {
                    if (await factory.TestAsync(url, cancellation).ConfigureAwait(false)) return factory;
                    if (cancellation.IsCancellationRequested) break;
                }
            } catch (Exception e) when (e.IsCancelled()) { }
            return null;
        }

        public static async Task<bool> IsSupportedAsync(string url, CancellationToken cancellation) {
            return await GetFactoryAsync(url, cancellation).ConfigureAwait(false) != null;
        }

        [ItemCanBeNull]
        public static Task<ILoader> CreateLoaderAsync([NotNull] string url, CancellationToken cancellation) {
            return CreateLoaderAsync(url, null, cancellation);
        }

        private static string UnwrapUrl([NotNull] string url) {
            if (CmRequestHandler?.Test(url) == true) {
                var unwrapped = CmRequestHandler.UnwrapDownloadUrl(url);
                if (unwrapped != null) {
                    Logging.Debug($"Unwrapped URL: {unwrapped}");
                    return unwrapped;
                }

                CmRequestHandler.Handle(url);
                throw new NotSupportedException("Link is handled by CM Requests Handler");
            }

            return (from unwrapping in DataProvider.Instance.UrlUnwrappings
                where unwrapping.Test.IsMatch(url)
                select new Uri(url, UriKind.Absolute).GetQueryParam(unwrapping.QueryParameter)).FirstOrDefault();
        }

        [ItemCanBeNull]
        public static async Task<ILoader> CreateLoaderAsync([NotNull] string url, [CanBeNull] ILoader parent, CancellationToken cancellation) {
            for (var i = 0; i < 10; ++i) {
                try {
                    var newUrl = UnwrapUrl(url);
                    if (newUrl != null && newUrl != url) {
                        url = newUrl;
                        continue;
                    }
                } catch (NotSupportedException) {
                    return null;
                } catch (Exception e) {
                    Logging.Warning($"URL: {url}, error: {e}");
                }
                break;
            }

            try {
                var factory = await GetFactoryAsync(url, cancellation);
                if (cancellation.IsCancellationRequested) return null;
                var loader = factory == null ? new DirectLoader(url) : await factory.CreateAsync(url, cancellation);
                if (loader == null) return null;
                loader.Parent = parent;
                return loader;
            } catch (Exception e) when (e.IsCancelled()) {
                return null;
            }
        }

        private static IWebProxy _proxy;

        public static void SetProxy(IWebProxy proxy) {
            _proxy = proxy;
        }

        public static async Task<string> UnwrapLink(string argument, CancellationToken cancellation = default) {
            var loader = await CreateLoaderAsync(argument, cancellation) ?? throw new OperationCanceledException();
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
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Finding fitting loader…"));
            var loader = await CreateLoaderAsync(argument, cancellation) ?? throw new OperationCanceledException();
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
                                    && loader.TotalSize.HasValue ? Math.Max(loader.TotalSize.Value, args.BytesReceived) : args.TotalBytesToReceive,
                                    progressStopwatch));
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