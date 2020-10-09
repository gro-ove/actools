using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Tools.Helpers.Api;
using B2Net;
using B2Net.Http;
using B2Net.Models;
using FirstFloor.ModernUI.Dialogs;
using Newtonsoft.Json.Linq;

namespace AcManager.Workshop.Uploaders {
    public class B2WorkshopUploader : IWorkshopUploader {
        private B2Client _b2Client;
        private string _b2ClientPrefix;
        private string _b2BucketName;
        private string _hostName;
        private string _currentGroup;

        public B2WorkshopUploader(JObject uploadParams) {
            HttpClientFactory.SetHttpClient(HttpClientHolder.Get());
            _b2Client = new B2Client(new B2Options {
                KeyId = uploadParams["keyID"].ToString(),
                ApplicationKey = uploadParams["keyValue"].ToString(),
                BucketId = uploadParams["bucketID"].ToString(),
                PersistBucket = true
            });
            _b2ClientPrefix = uploadParams["prefix"].ToString();
            _b2BucketName = uploadParams["bucketName"].ToString();
            _hostName = uploadParams["hostName"].ToString();
        }

        public void MarkNewGroup() {
            _currentGroup = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        async Task<string> IWorkshopUploader.UploadAsync(byte[] data, string downloadName, string origin, IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation) {
            await _b2Client.Authorize(cancellation);
            if (cancellation.IsCancellationRequested) {
                throw new TaskCanceledException();
            }

            if (_currentGroup == null) {
                MarkNewGroup();
            }

            var fileName = _b2ClientPrefix + _currentGroup + "/" + downloadName;
            var uploadUrl = await _b2Client.Files.GetUploadUrl().ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) throw new TaskCanceledException();

            var stopwatch = new AsyncProgressBytesStopwatch();
            var file = await _b2Client.Files.Upload(data, fileName, uploadUrl, "", "", new Dictionary<string, string> {
                ["b2-content-disposition"] = Regex.IsMatch(downloadName, @"\.(png|jpe?g)$") ? "inline" : "attachment",
                ["b2-cache-control"] = "immutable",
                ["x-origin"] = origin == null ? "" :HttpUtility.UrlEncode(origin),
            }, progress == null ? null : new Progress<long>(x => {
                progress.Report(AsyncProgressEntry.CreateUploading(x, data.Length, stopwatch));
            }), cancellation).ConfigureAwait(false);
            return $"{_hostName}/file/{_b2BucketName}/{file.FileName}";
        }
    }
}