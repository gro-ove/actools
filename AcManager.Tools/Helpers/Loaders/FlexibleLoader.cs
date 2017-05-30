using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public static class FlexibleLoader {
        internal static ILoader CreateLoader(string uri) {
            if (GoogleDriveLoader.Test(uri)) return new GoogleDriveLoader(uri);
            if (YandexDiskLoader.Test(uri)) return new YandexDiskLoader(uri);
            if (AcClubLoader.Test(uri)) return new AcClubLoader(uri);
            if (RaceDepartmentLoader.Test(uri)) return new RaceDepartmentLoader(uri);
            if (AssettoDbLoader.Test(uri)) return new AssettoDbLoader(uri);
            return new DirectLoader(uri);
        }

        [ItemCanBeNull]
        public static Task<string> TryToLoadAsync(string argument, string name = null, string extension = null, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var tmp = name == null
                    ? extension == null ? Path.GetTempFileName() : FileUtils.GetTempFileName(Path.GetTempPath(), extension)
                    : FileUtils.GetTempFileNameFixed(Path.GetTempPath(), name);
            return TryToLoadAsyncTo(argument, tmp, progress, cancellation);
        }

        [ItemCanBeNull]
        public static async Task<string> TryToLoadAsyncTo(string argument, string destination, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            try {
                return await LoadAsyncTo(argument, destination, progress, cancellation).ConfigureAwait(false);
            } catch (TaskCanceledException) {
                return null;
            } catch (WebException) when (cancellation.IsCancellationRequested) {
                return null;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.Common_CannotDownloadFile_Commentary, e);
                return null;
            }
        }

        [ItemNotNull]
        public static Task<string> LoadAsync(string argument, string name = null, string extension = null, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var tmp = name == null
                    ? extension == null ? Path.GetTempFileName() : FileUtils.GetTempFileName(Path.GetTempPath(), extension)
                    : FileUtils.GetTempFileNameFixed(Path.GetTempPath(), name);
            return LoadAsyncTo(argument, tmp, progress, cancellation);
        }

        [ItemNotNull]
        public static async Task<string> LoadAsyncTo(string argument, string destination, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var loader = CreateLoader(argument);

            using (var order = KillerOrder.Create(new CookieAwareWebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                }
            }, TimeSpan.FromMinutes(2))) {
                var client = order.Victim;
                progress?.Report(AsyncProgressEntry.Indetermitate);

                cancellation.ThrowIfCancellationRequested();
                cancellation.Register(client.CancelAsync);

                if (!await loader.PrepareAsync(client, cancellation)) {
                    throw new InformativeException("Can’t load file", "Loader preparation failed.");
                }

                cancellation.ThrowIfCancellationRequested();

                var s = Stopwatch.StartNew();
                client.DownloadProgressChanged += (sender, args) => {
                    // ReSharper disable once AccessToDisposedClosure

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

                    // ReSharper disable once AccessToDisposedClosure
                    progress?.Report(AsyncProgressEntry.CreateDownloading(args.BytesReceived, total));
                };

                await loader.DownloadAsync(client, destination, cancellation);
                cancellation.ThrowIfCancellationRequested();
            }

            return destination;
        }
    }
}
