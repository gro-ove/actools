using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Loaders {
    public class DirectLoader : ILoader {
        private readonly string _keyDestination;
        private readonly string _keyPartiallyLoadedFilename;
        private readonly string _keyFootprint;
        private readonly string _keyLastWriteDate;
        protected string Url;

        public long? TotalSize { get; protected set; }
        public string Version { get; protected set; }
        public string FileName { get; protected set; }

        public DirectLoader(string url) {
            _keyDestination = ".DirectLoader.Destination:" + url;
            _keyPartiallyLoadedFilename = ".DirectLoader.PartiallyLoadedFilename:" + url;
            _keyFootprint = ".DirectLoader.Footprint:" + url;
            _keyLastWriteDate = ".DirectLoader.LastWriteDate:" + url;

            if (GetType() == typeof(DirectLoader)) {
                FileName = Path.GetFileName(url)?.Split('?', '&')[0];
                if (FileName == "") FileName = null;
            }

            Url = url;
        }

        public virtual Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            return Task.FromResult(true);
        }

        public Task<string> DownloadAsync(CookieAwareWebClient client, FlexibleLoaderDestinationCallback destinationCallback, IProgress<long> progress,
                CancellationToken cancellation) {
            return DownloadAsyncInner(client, destinationCallback, progress, cancellation);
        }

        private static bool TryDecode5987(string input, out string output) {
            output = null;

            var quoteIndex = input.IndexOf('\'');
            if (quoteIndex == -1) {
                return false;
            }

            var lastQuoteIndex = input.LastIndexOf('\'');
            if (quoteIndex == lastQuoteIndex || input.IndexOf('\'', quoteIndex + 1) != lastQuoteIndex) {
                return false;
            }

            var encodingString = input.Substring(0, quoteIndex);
            var dataString = input.Substring(lastQuoteIndex + 1, input.Length - (lastQuoteIndex + 1));

            var decoded = new StringBuilder();
            try {
                var encoding = Encoding.GetEncoding(encodingString);
                var unescapedBytes = new byte[dataString.Length];
                var unescapedBytesCount = 0;
                for (var index = 0; index < dataString.Length; index++) {
                    if (Uri.IsHexEncoding(dataString, index)) {
                        unescapedBytes[unescapedBytesCount++] = (byte)Uri.HexUnescape(dataString, ref index);
                        index--;
                    } else {
                        if (unescapedBytesCount > 0) {
                            decoded.Append(encoding.GetString(unescapedBytes, 0, unescapedBytesCount));
                            unescapedBytesCount = 0;
                        }
                        decoded.Append(dataString[index]);
                    }
                }

                if (unescapedBytesCount > 0) {
                    decoded.Append(encoding.GetString(unescapedBytes, 0, unescapedBytesCount));
                }
            } catch (ArgumentException) {
                return false;
            }

            output = decoded.ToString();
            return true;
        }

        private static bool TryGetFileName(WebHeaderCollection headers, out string filename) {
            try {
                var contentDisposition = headers["Content-Disposition"]?.Split(';').Select(x => x.Split(new[] { '=' }, 2)).Where(x => x.Length == 2)
                                                                        .ToDictionary(x => x[0].Trim().ToLowerInvariant(), x => x[1].Trim());
                if (contentDisposition != null) {
                    if (contentDisposition.TryGetValue("filename", out var value)) {
                        filename = JsonConvert.DeserializeObject<string>(value);
                        return true;
                    }

                    if (contentDisposition.TryGetValue("filename*", out var filenameStar)) {
                        filename = TryDecode5987(filenameStar, out var decoded) ? decoded : null;
                        return true;
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            filename = null;
            return false;
        }

        public bool UsesClientToDownload => false;
        protected virtual bool? ResumeSupported => false;
        protected virtual bool HeadRequestSupported => true;

        protected virtual Task<string> DownloadAsyncInner([NotNull] CookieAwareWebClient client,
                [NotNull] FlexibleLoaderDestinationCallback destinationCallback, IProgress<long> progress, CancellationToken cancellation) {
            return UsesClientToDownload
                    ? DownloadClientAsync(client, destinationCallback, cancellation)
                    : DownloadResumeSupportAsync(client, destinationCallback, progress, cancellation);
        }

        private async Task<string> DownloadClientAsync([NotNull] CookieAwareWebClient client, [NotNull] FlexibleLoaderDestinationCallback destinationCallback,
                CancellationToken cancellation) {
            cancellation.Register(client.CancelAsync);
            var information = FlexibleLoaderMetaInformation.FromLoader(this);
            var destination = destinationCallback(Url, information);
            var filename = FileUtils.EnsureUnique(true, destination.Filename);
            await client.DownloadFileTaskAsync(Url, filename).ConfigureAwait(false);
            return filename;
        }

        private async Task<string> DownloadResumeSupportAsync([NotNull] CookieAwareWebClient client, [NotNull] FlexibleLoaderDestinationCallback destinationCallback,
                IProgress<long> progress, CancellationToken cancellation) {
            // Common variables
            string filename = null, selectedDestination = null, actualFootprint = null;
            Stream remoteData = null;

            var resumeSupported = ResumeSupported;

            try {
                // Read resume-related data and remove it to avoid conflicts
                var resumeDestination = CacheStorage.GetString(_keyDestination);
                var resumePartiallyLoadedFilename = CacheStorage.GetString(_keyPartiallyLoadedFilename);
                var resumeLastWriteDate = CacheStorage.GetDateTime(_keyLastWriteDate);
                var resumePreviousFootprint = CacheStorage.GetString(_keyFootprint);
                ClearResumeData();

                // Collect known information for destination callback
                var information = FlexibleLoaderMetaInformation.FromLoader(this);

                // Opening stream to read…
                remoteData = await client.OpenReadTaskAsync(Url);
                cancellation.ThrowIfCancellationRequested();

                // Maybe we’ll be lucky enough to load the most accurate data
                if (client.ResponseHeaders != null) {
                    if (long.TryParse(client.ResponseHeaders[HttpResponseHeader.ContentLength],
                            NumberStyles.Any, CultureInfo.InvariantCulture, out var length)) {
                        information.TotalSize = length;
                    }

                    if (TryGetFileName(client.ResponseHeaders, out var fileName)) {
                        information.FileName = fileName;
                    }

                    if (resumeSupported == null && client.ResponseHeaders[HttpResponseHeader.AcceptRanges].Contains("bytes")) {
                        // We could check for “Accept-Ranges: none” here, but, for example, Google Drive
                        // responds with “none” and yet allows to download file partially.
                        resumeSupported = true;
                    }

                    client.LogResponseHeaders();
                }

                // Was the file partially loaded before?
                var partiallyLoaded = ResumeSupported != false && resumePartiallyLoadedFilename != null
                        ? new FileInfo(FileUtils.EnsureFilenameIsValid(resumePartiallyLoadedFilename)) : null;
                if (partiallyLoaded != null) {
                    Logging.Warning("Not finished: " + partiallyLoaded);
                }

                // Does it still exist
                if (partiallyLoaded?.Exists != true) {
                    Logging.Warning("Partially downloaded file does not exist");
                    partiallyLoaded = null;
                }

                // If so, wasn’t it changed since the last time?
                if (partiallyLoaded?.LastWriteTime > resumeLastWriteDate + TimeSpan.FromMinutes(5)) {
                    Logging.Warning($"Partially downloaded file is newer that it should be: {partiallyLoaded.LastWriteTime}, expected: {resumeLastWriteDate}");
                    partiallyLoaded = null;
                }

                // Looks like file is partially downloaded, but let’s ensure link still leads to the same content
                actualFootprint = information.GetFootprint();
                if (partiallyLoaded != null && resumePreviousFootprint != actualFootprint) {
                    Logging.Warning($"Footprints don’t match: {resumePreviousFootprint}≠{actualFootprint}");
                    partiallyLoaded = null;
                }

                // Let’s check where to load data, which is potentially the most actual data at this point
                var destination = destinationCallback(Url, information);
                selectedDestination = destination.Filename;
                if (partiallyLoaded != null && (!destination.CanResumeDownload || !FileUtils.ArePathsEqual(selectedDestination, resumeDestination))) {
                    Logging.Warning($"Different destination chosen: {selectedDestination} (before: {resumeDestination})");
                    partiallyLoaded = null;
                }

                // TODO: Check that header?
                // TODO: Use proper HEAD request?
                // TODO: Check for array-related bytes

                // Where to write?
                filename = partiallyLoaded?.FullName ?? FileUtils.EnsureUnique(true, destination.Filename);

                // Set cancellation token
                cancellation.Register(o => client.CancelAsync(), null);

                // Open write stream
                if (partiallyLoaded != null) {
                    var rangeFrom = partiallyLoaded.Length;
                    using (var file = new FileStream(filename, FileMode.Append, FileAccess.Write))
                    using (client.SetRange(new Tuple<long, long>(rangeFrom, -1))) {
                        Logging.Warning($"Trying to resume download from {rangeFrom} bytes…");

                        remoteData.Dispose();
                        remoteData = await client.OpenReadTaskAsync(Url);

                        // It’s unknown if resume is supported or not at this point
                        if (resumeSupported == null) {
                            var bytes = new byte[16];
                            var firstBytes = await remoteData.ReadAsync(bytes, 0, bytes.Length);
                            rangeFrom += 16;
                        }

                        client.LogResponseHeaders();
                        await remoteData.CopyToAsync(file, bufferSize: 65536, progress: new Progress<long>(v => {
                            progress?.Report(v + rangeFrom);
                        }), cancellation: cancellation);
                    }
                } else {
                    using (var file = File.OpenWrite(filename)) {
                        Logging.Debug("Downloading the whole file…");
                        await remoteData.CopyToAsync(file, bufferSize: 65536, progress: progress, cancellation: cancellation);
                    }
                }

                cancellation.ThrowIfCancellationRequested();
                Logging.Write("Download finished");
                return filename;
            } catch (Exception e) when (e is WebException || e.IsCanceled()) {
                Logging.Write("Download is interrupted! Saving details to resume later…");
                var download = filename == null ? null : new FileInfo(filename);
                if (download?.Exists == true && filename.Length > 0) {
                    CacheStorage.Set(_keyDestination, selectedDestination);
                    CacheStorage.Set(_keyPartiallyLoadedFilename, filename);
                    CacheStorage.Set(_keyFootprint, actualFootprint);
                    CacheStorage.Set(_keyLastWriteDate, download.LastWriteTime);
                } else {
                    ClearResumeData();
                }

                throw;
            } finally {
                remoteData?.Dispose();
            }
        }

        private void ClearResumeData() {
            CacheStorage.Remove(_keyDestination);
            CacheStorage.Remove(_keyPartiallyLoadedFilename);
            CacheStorage.Remove(_keyFootprint);
            CacheStorage.Remove(_keyLastWriteDate);
        }

        public virtual Task<string> GetDownloadLink(CancellationToken cancellation) {
            return Task.FromResult(Url);
        }
    }
}