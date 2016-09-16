using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    public static class FlexibleLoader {
        internal static ILoader CreateLoader(string uri) {
            if (GoogleDriveLoader.Test(uri)) return new GoogleDriveLoader(uri);
            if (AcClubLoader.Test(uri)) return new AcClubLoader(uri);
            if (RaceDepartmentLoader.Test(uri)) return new RaceDepartmentLoader(uri);
            if (AssettoDbLoader.Test(uri)) return new AssettoDbLoader(uri);
            return new DirectLoader(uri);
        }

        public static async Task<string> LoadAsync(string argument, string name = null, string extension = null, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var tmp = name == null
                ? extension == null ? Path.GetTempFileName() : FileUtils.GetTempFileName(Path.GetTempPath(), extension)
                : FileUtils.GetTempFileNameFixed(Path.GetTempPath(), name);
            var loader = CreateLoader(argument);

            try {
                // TODO: Timeout?
                using (var client = new CookieAwareWebClient {
                    Headers = {
                        [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                    }
                }) {
                    progress?.Report(AsyncProgressEntry.Indetermitate);

                    cancellation.ThrowIfCancellationRequested();
                    cancellation.Register(client.CancelAsync);

                    if (!await loader.PrepareAsync(client, cancellation) ||
                            cancellation.IsCancellationRequested) return null;

                    var skipEvent = 0;
                    client.DownloadProgressChanged += (sender, args) => {
                        if (++skipEvent > 50) {
                            skipEvent = 0;
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

                    await loader.DownloadAsync(client, tmp, cancellation);
                    if (cancellation.IsCancellationRequested) return null;

                    Logging.Write("Result: " + tmp);
                }

                return tmp;
            } catch (TaskCanceledException) {
                return null;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.Common_CannotDownloadFile_Commentary, e);
                return null;
            }
        }
    }
}
