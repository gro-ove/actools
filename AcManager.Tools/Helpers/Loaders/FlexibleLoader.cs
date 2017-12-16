using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ICmRequestHandler {
        bool Test([NotNull] string request);

        [CanBeNull]
        string UnwrapDownloadUrl([NotNull] string request);

        void Handle([NotNull] string request);
    }

    public static class FlexibleLoader {
        [CanBeNull]
        public static ICmRequestHandler CmRequestHandler { get; set; }

        [NotNull]
        public static ILoader CreateLoader([NotNull] string uri) {
            if (CmRequestHandler?.Test(uri) == true) {
                var unwrapped = CmRequestHandler.UnwrapDownloadUrl(uri);
                if (unwrapped != null) {
                    uri = unwrapped;
                    Logging.Debug($"Unwrapped URL: {unwrapped}");
                } else {
                    throw new OperationCanceledException("Link is handled by CM Requests Handler");
                }
            }

            if (AcStuffSharedLoader.Test(uri)) return new AcStuffSharedLoader(uri);
            if (GoogleDriveLoader.Test(uri)) return new GoogleDriveLoader(uri);
            if (YandexDiskLoader.Test(uri)) return new YandexDiskLoader(uri);
            if (MediaFireLoader.Test(uri)) return new MediaFireLoader(uri);
            if (DropboxLoader.Test(uri)) return new DropboxLoader(uri);
            if (OneDriveLoader.Test(uri)) return new OneDriveLoader(uri);
            if (AcClubLoader.Test(uri)) return new AcClubLoader(uri);
            if (AcDriftingProLoader.Test(uri)) return new AcDriftingProLoader(uri);
            if (RaceDepartmentLoader.Test(uri)) return new RaceDepartmentLoader(uri);
            if (AssettoDbLoader.Test(uri)) return new AssettoDbLoader(uri);
            if (AdFlyLoader.Test(uri)) return new AdFlyLoader(uri);
            if (MegaLoader.Test(uri)) return new MegaLoader(uri);
            return new DirectLoader(uri);
        }

        /*
        private static string GetTemporaryName(string argument) {
            using (var sha1 = new SHA1Managed()) {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(argument));
                return BitConverter.ToString(hash).Replace(@"-", "").ToLower();
            }
        }

        [ItemNotNull]
        public static async Task<string> LoadAsync(string argument, string name = null, string extension = null, bool useCachedIfAny = false,
                IProgress<AsyncProgressEntry> progress = null, Action<FlexibleLoaderMetaInformation> metaInformationCallback = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var directory = SettingsHolder.Content.TemporaryFilesLocationValue;
            if (useCachedIfAny) {
                var fileName = name ?? $"cm_dl_{GetTemporaryName(argument)}{extension}";
                var destination = Path.Combine(directory, fileName);
                if (File.Exists(destination)) return destination;

                var temporary = destination + ".tmp";
                await LoadAsyncTo(argument, temporary, progress, metaInformationCallback, cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();

                if (File.Exists(temporary)) {
                    File.Move(temporary, destination);
                    return destination;
                }

                throw new Exception("Downloaded file is missing");
            } else {
                string destination;
                if (name != null) {
                    destination = Path.Combine(directory, name);
                    if (File.Exists(destination)) {
                        destination = FileUtils.GetTempFileNameFixed(directory, name);
                    }
                } else {
                    destination = extension == null
                            ? FileUtils.GetTempFileName(directory)
                            : FileUtils.GetTempFileName(directory, extension);
                }

                File.WriteAllBytes(destination, new byte[0]);
                await LoadAsyncTo(argument, destination, progress, metaInformationCallback, cancellation).ConfigureAwait(false);
                return destination;
            }
        }*/

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

        public static async Task<string> LoadAsyncTo(string argument, FlexibleLoaderDestinationCallback destinationCallback,
                IProgress<AsyncProgressEntry> progress = null, Action<FlexibleLoaderMetaInformation> metaInformationCallback = null,
                CancellationToken cancellation = default(CancellationToken)) {
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
                    metaInformationCallback?.Invoke(FlexibleLoaderMetaInformation.FromLoader(loader));

                    var initialProgressCallback = true;

                    var s = Stopwatch.StartNew();
                    if (loader.UsesClientToDownload) {
                        client.DownloadProgressChanged += (sender, args) => {
                            if (initialProgressCallback) {
                                metaInformationCallback?.Invoke(FlexibleLoaderMetaInformation.FromLoader(loader));
                                initialProgressCallback = false;
                            }

                            if (s.Elapsed.TotalMilliseconds < 20) return;
                            order.Delay();
                            s.Restart();
                            progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, args.TotalBytesToReceive == -1
                                    && loader.TotalSize.HasValue ? Math.Max(loader.TotalSize.Value, args.BytesReceived) : args.TotalBytesToReceive));
                        };
                    }

                    var loaded = await loader.DownloadAsync(client, destinationCallback, loader.UsesClientToDownload ? null : new Progress<long>(p => {
                        if (initialProgressCallback) {
                            metaInformationCallback?.Invoke(FlexibleLoaderMetaInformation.FromLoader(loader));
                            initialProgressCallback = false;
                        }

                        if (s.Elapsed.TotalMilliseconds < 20) return;
                        order.Delay();
                        s.Restart();
                        progress?.Report(loader.TotalSize.HasValue ? AsyncProgressEntry.CreateDownloading(p, loader.TotalSize.Value)
                                : new AsyncProgressEntry(string.Format(UiStrings.Progress_Downloading, p.ToReadableSize(1)), null));
                    }), cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    Logging.Write("Loaded: " + loaded);
                    return loaded;
                }
            } catch (Exception e) when (cancellation.IsCancellationRequested || e.IsCanceled()) {
                Logging.Warning("Cancelled");
                throw new OperationCanceledException();
            } catch (Exception e) {
                Logging.Warning(e);
                throw;
            }
        }
    }
}