using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public class FlexibleLoaderMetaInformation {
        public FlexibleLoaderMetaInformation(long totalSize, string fileName, string version) {
            TotalSize = totalSize;
            FileName = fileName;
            Version = version;
        }

        public long TotalSize { get; }
        public string FileName { get; }
        public string Version { get; }
    }

    public static class FlexibleLoader {
        public static ILoader CreateLoader(string uri) {
            if (GoogleDriveLoader.Test(uri)) return new GoogleDriveLoader(uri);
            if (YandexDiskLoader.Test(uri)) return new YandexDiskLoader(uri);
            if (MediaFireLoader.Test(uri)) return new MediaFireLoader(uri);
            if (DropboxLoader.Test(uri)) return new DropboxLoader(uri);
            if (OneDriveLoader.Test(uri)) return new OneDriveLoader(uri);
            if (AcClubLoader.Test(uri)) return new AcClubLoader(uri);
            if (RaceDepartmentLoader.Test(uri)) return new RaceDepartmentLoader(uri);
            if (AssettoDbLoader.Test(uri)) return new AssettoDbLoader(uri);
            if (AdFlyLoader.Test(uri)) return new AdFlyLoader(uri);
            if (MegaLoader.Test(uri)) return new MegaLoader(uri);
            return new DirectLoader(uri);
        }

        private static string GetTemporaryName(string argument) {
            using (var sha1 = new SHA1Managed()) {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(argument));
                return BitConverter.ToString(hash).Replace(@"-", "").ToLower();
            }
        }

        [ItemNotNull]
        public static async Task<string> LoadAsync(string argument, string name = null, string extension = null, bool useCachedIfAny = false,
                string directory = null, IProgress<AsyncProgressEntry> progress = null, Action<FlexibleLoaderMetaInformation> metaInformationCallback = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var fixedDirectory = directory != null;
            if (!fixedDirectory) {
                directory = SettingsHolder.Content.TemporaryFilesLocationValue;
            }

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
                    if (!fixedDirectory && File.Exists(destination)) {
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
        }

        private static IWebProxy _proxy;

        public static void SetProxy(IWebProxy proxy) {
            _proxy = proxy;
        }

        public static async Task<string> UnwrapLink(string argument, CancellationToken cancellation = default(CancellationToken)) {
            var loader = CreateLoader(argument);
            using (var order = KillerOrder.Create(new CookieAwareWebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                }
            }, TimeSpan.FromMinutes(10))) {
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

        public static async Task LoadAsyncTo(string argument, string destination, IProgress<AsyncProgressEntry> progress = null,
                Action<FlexibleLoaderMetaInformation> metaInformationCallback = null, CancellationToken cancellation = default(CancellationToken)) {
            var loader = CreateLoader(argument);

            try {
                using (var order = KillerOrder.Create(new CookieAwareWebClient {
                    Headers = {
                        [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                    }
                }, TimeSpan.FromMinutes(10))) {
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
                    metaInformationCallback?.Invoke(new FlexibleLoaderMetaInformation(loader.TotalSize, loader.FileName, loader.Version));

                    var s = Stopwatch.StartNew();
                    if (loader.UsesClientToDownload) {
                        client.DownloadProgressChanged += (sender, args) => {
                            if (s.Elapsed.TotalMilliseconds > 20) {
                                order.Delay();
                                s.Restart();
                            } else {
                                return;
                            }

                            var total = args.TotalBytesToReceive;
                            if (total == -1 && loader.TotalSize != -1) {
                                total = Math.Max(loader.TotalSize, args.BytesReceived);
                            }

                            progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, total));
                        };
                    }

                    await loader.DownloadAsync(client, destination, loader.UsesClientToDownload ? null : new Progress<double>(p => {
                        if (s.Elapsed.TotalMilliseconds > 20) {
                            order.Delay();
                            s.Restart();
                        } else {
                            return;
                        }

                        var total = loader.TotalSize;
                        progress?.Report(total == -1 ?
                                new AsyncProgressEntry("Loading…", p) :
                                AsyncProgressEntry.CreateDownloading((long)(p * total), total));
                    }), cancellation);
                    cancellation.ThrowIfCancellationRequested();
                }
            } catch (Exception) when (cancellation.IsCancellationRequested) {
                throw new OperationCanceledException();
            }
        }
    }
}
