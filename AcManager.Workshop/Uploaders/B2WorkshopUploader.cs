using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using B2Net;
using B2Net.Http;
using B2Net.Models;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop.Uploaders {
    public class B2WorkshopUploader : IWorkshopUploader {
        private static B2Client _b2ClientLast;
        private static string _b2ClientConfig;

        private readonly B2Client _b2Client;
        private readonly string _b2ClientPrefix;

        public B2WorkshopUploader(JObject uploadParams) {
            HttpClientFactory.SetHttpClient(HttpClientHolder.Get());

            var config = uploadParams.ToString();
            if (_b2ClientConfig == config) {
                _b2Client = _b2ClientLast;
            } else {
                _b2Client = new B2Client(new B2Options {
                    KeyId = uploadParams["keyID"].ToString(),
                    ApplicationKey = uploadParams["keyValue"].ToString(),
                    BucketId = uploadParams["bucketID"].ToString(),
                    PersistBucket = true
                });
                _b2ClientLast = _b2Client;
                _b2ClientConfig = config;
            }
            _b2ClientPrefix = uploadParams["prefix"].ToString();
        }

        private Task _authorizing;
        private object _authorizingLock = new object();

        private async Task AuthorizeInner() {
            await _b2Client.Authorize().ConfigureAwait(false);
            lock (_authorizingLock) {
                _authorizing = null;
            }
        }

        private Task Authorize() {
            lock (_authorizingLock) {
                return _authorizing ?? (_authorizing = AuthorizeInner());
            }
        }

        public async Task<WorkshopUploadResult> UploadAsync(byte[] data, string group, string name,
                IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Authorizing…"));
            await Authorize().WithCancellation(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();

            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Finding a vault to upload…"));
            var uploadUrl = await _b2Client.Files.GetUploadUrl(cancelToken: cancellation);
            cancellation.ThrowIfCancellationRequested();

            for (var i = 0; i < 4; ++i) {
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate(i == 0
                        ? "Starting upload…"
                        : $"Trying again, {(i + 1).ToOrdinal("attempt").ToSentenceMember()} attempt"));
                try {
                    return await TryToUploadAsync(uploadUrl);
                } catch (B2Exception e) when (e.Code == "bad_auth_token" || e.ShouldRetryRequest) {
                    cancellation.ThrowIfCancellationRequested();
                    uploadUrl = await _b2Client.Files.GetUploadUrl(cancelToken: cancellation);
                } catch (HttpRequestException e) {
                    Logging.Warning(e);
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Target vault is not available, waiting a bit before the next attempt…"));
                    await Task.Delay(TimeSpan.FromSeconds(i + 1d));
                    cancellation.ThrowIfCancellationRequested();
                } catch (WebException e) {
                    Logging.Warning(e);
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Target vault is not available, waiting a bit before the next attempt…"));
                    await Task.Delay(TimeSpan.FromSeconds(i + 1d));
                    cancellation.ThrowIfCancellationRequested();
                } catch (B2Exception e) when (e.Status == "500" || e.Status == "503") {
                    Logging.Warning(e);
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Target vault is not full, waiting a bit before the next attempt…"));
                    await Task.Delay(TimeSpan.FromSeconds(i + 1d));
                    cancellation.ThrowIfCancellationRequested();
                } catch (B2Exception e) {
                    Logging.Warning("B2Exception.Code=" + e.Code);
                    Logging.Warning("B2Exception.Status=" + e.Status);
                    Logging.Warning("B2Exception.Message=" + e.Message);
                    Logging.Warning("B2Exception.ShouldRetryRequest=" + e.ShouldRetryRequest);
                    throw;
                }
            }

            cancellation.ThrowIfCancellationRequested();
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate($"Trying again, last attempt"));
            return await TryToUploadAsync(uploadUrl);

            async Task<WorkshopUploadResult> TryToUploadAsync(B2UploadUrl url) {
                var fileName = _b2ClientPrefix + group + "/" + name;
                var stopwatch = new AsyncProgressBytesStopwatch();
                var file = await _b2Client.Files.Upload(data, fileName, url, "", "", new Dictionary<string, string> {
                    ["b2-content-disposition"] = Regex.IsMatch(name, @"\.(png|jpg)$") ? "inline" : "attachment",
                    ["b2-cache-control"] = "immutable"
                }, progress == null ? null : new Progress<long>(x => progress.Report(AsyncProgressEntry.CreateUploading(x, data.Length, stopwatch))),
                        cancellation).ConfigureAwait(false);
                return new WorkshopUploadResult {
                    Tag = JsonConvert.SerializeObject(new { fileName = file.FileName, fileID = file.FileId }),
                    Size = data.LongLength
                };
            }
        }
    }
}