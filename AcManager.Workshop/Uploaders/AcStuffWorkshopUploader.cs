using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using B2Net;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop.Uploaders {
    public class AcStuffWorkshopUploader : IWorkshopUploader {
        private readonly string _endpoint;
        private readonly string _checksum;

        public AcStuffWorkshopUploader(JObject uploadParams) {
            _endpoint = uploadParams["endpoint"].ToString();
            _checksum = uploadParams["checksum"].ToString();
        }

        public async Task<WorkshopUploadResult> UploadAsync(byte[] data, string group, string name,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            for (var i = 0; i < 3; ++i) {
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate(i == 0
                        ? "Starting upload…"
                        : $"Trying again, {(i + 1).ToOrdinal("attempt").ToSentenceMember()} attempt"));
                try {
                    return await TryToUploadAsync();
                } catch (HttpRequestException e) {
                    Logging.Warning(e);
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Upload is failed, waiting a bit before the next attempt…"));
                    await Task.Delay(TimeSpan.FromSeconds(i + 1d));
                    cancellation.ThrowIfCancellationRequested();
                } catch (WebException e) {
                    Logging.Warning(e);
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Upload is failed, waiting a bit before the next attempt…"));
                    await Task.Delay(TimeSpan.FromSeconds(i + 1d));
                    cancellation.ThrowIfCancellationRequested();
                }
            }

            cancellation.ThrowIfCancellationRequested();
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate($"Trying again, last attempt"));
            return await TryToUploadAsync();

            async Task<WorkshopUploadResult> TryToUploadAsync() {
                var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                request.Headers.TryAddWithoutValidation("X-Data-File-Group", group);
                request.Headers.TryAddWithoutValidation("X-Data-File-Name", name);
                request.Headers.TryAddWithoutValidation("X-Data-Checksum", _checksum);
                var stopwatch = new AsyncProgressBytesStopwatch();
                request.Content = progress == null
                        ? (HttpContent)new ByteArrayContent(data)
                        : new ProgressableByteArrayContent(data, 8192,
                                new Progress<long>(x => progress.Report(AsyncProgressEntry.CreateUploading(x, data.Length, stopwatch))));
                using (var response = await HttpClientHolder.Get().SendAsync(request, cancellation).ConfigureAwait(false)) {
                    if (response.StatusCode != HttpStatusCode.OK) {
                        throw new WebException($"Failed to upload: {response.StatusCode}, response: {await LoadContent()}");
                    }
                    var result = JObject.Parse(await LoadContent());
                    return new WorkshopUploadResult {
                        Size = data.Length,
                        Tag = result["key"].ToString()
                    };

                    ConfiguredTaskAwaitable<string> LoadContent() {
                        return response.Content.ReadAsStringAsync().WithCancellation(cancellation).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}