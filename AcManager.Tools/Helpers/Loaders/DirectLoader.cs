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
        public int BufferSize { get; set; } = 65536;

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

        public Task<string> DownloadAsync(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            return DownloadAsyncInner(client, getPreferredDestination, reportDestination, checkIfPaused, progress, cancellation);
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
        public bool CanPause => !UsesClientToDownload;
        protected virtual bool? ResumeSupported => null;
        protected virtual bool HeadRequestSupported => true;

        protected virtual Task<string> DownloadAsyncInner([NotNull] CookieAwareWebClient client,
                [NotNull] FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                [CanBeNull] FlexibleLoaderReportDestinationCallback reportDestination, [CanBeNull] Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            return UsesClientToDownload
                    ? DownloadClientAsync(client, getPreferredDestination, reportDestination, checkIfPaused, cancellation)
                    : DownloadResumeSupportAsync(client, getPreferredDestination, reportDestination, checkIfPaused, progress, cancellation);
        }

        private async Task<string> DownloadClientAsync([NotNull] CookieAwareWebClient client,
                [NotNull] FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                [CanBeNull] FlexibleLoaderReportDestinationCallback reportDestination, [CanBeNull] Func<bool> checkIfPaused,
                CancellationToken cancellation) {
            cancellation.Register(client.CancelAsync);
            var information = FlexibleLoaderMetaInformation.FromLoader(this);
            var destination = getPreferredDestination(Url, information);
            var filename = FileUtils.EnsureUnique(true, destination.Filename);
            reportDestination?.Invoke(filename);
            await client.DownloadFileTaskAsync(Url, filename).ConfigureAwait(false);
            return filename;
        }

        public virtual string GetFootprint(FlexibleLoaderMetaInformation information, [CanBeNull] WebHeaderCollection headers) {
            return $"filename={information.FileName}, size={information.TotalSize}".ToCutBase64();
        }

        private static bool CouldBeBeginningOfAFile(byte[] bytes) {
            // RAR archive v5.0+
            if (bytes.StartsWith(new byte[] { (byte)'R', (byte)'a', (byte)'r', (byte)'!', 0x1A, 0x07, 0x01, 0x00 })) return true;

            // RAR archive v1.5+
            if (bytes.StartsWith(new byte[] { (byte)'R', (byte)'a', (byte)'r', (byte)'!', 0x1A, 0x07, 0x00 })) return true;

            // RAR archive v1.5+
            if (bytes.StartsWith(new byte[] { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C })) return true;

            // GZIP
            // if (bytes.StartsWith(new byte[] { 0x1F, 0x8B })) return true;

            // ZIP
            if (bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) return true;

            // TAR archive
            if (bytes.StartsWith(new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72, 0x00, 0x30, 0x30 })
                    || bytes.StartsWith(new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72, 0x20, 0x20, 0x00 })) return true;

            return false;
        }

        private async Task<string> DownloadResumeSupportAsync([NotNull] CookieAwareWebClient client,
                [NotNull] FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                [CanBeNull] FlexibleLoaderReportDestinationCallback reportDestination, [CanBeNull] Func<bool> checkIfPaused,
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
                var headRequest = HeadRequestSupported && resumeDestination != null;
                using (headRequest ? client.SetMethod("HEAD") : null) {
                    Logging.Warning($"Initial request: {(headRequest ? "HEAD" : "GET")}");
                    remoteData = await client.OpenReadTaskAsync(Url);
                }

                cancellation.ThrowIfCancellationRequested();

                // Maybe we’ll be lucky enough to load the most accurate data
                if (client.ResponseHeaders != null) {
                    if (long.TryParse(client.ResponseHeaders[HttpResponseHeader.ContentLength],
                            NumberStyles.Any, CultureInfo.InvariantCulture, out var length)) {
                        TotalSize = information.TotalSize = length;
                    }

                    if (TryGetFileName(client.ResponseHeaders, out var fileName)) {
                        FileName = information.FileName = fileName;
                    }

                    // For example, Google Drive responds with “none” and yet allows to download file partially,
                    // so this header will only be checked if value is not defined.
                    if (resumeSupported == null) {
                        var accept = client.ResponseHeaders[HttpResponseHeader.AcceptRanges];
                        if (accept.Contains("bytes")) {
                            resumeSupported = true;
                        } else if (accept.Contains("none")) {
                            resumeSupported = false;
                        }
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
                    Logging.Warning($"Partially downloaded file “{partiallyLoaded?.FullName}” does not exist");
                    partiallyLoaded = null;
                }

                // If so, wasn’t it changed since the last time?
                if (partiallyLoaded?.LastWriteTime > resumeLastWriteDate + TimeSpan.FromMinutes(5)) {
                    Logging.Warning($"Partially downloaded file is newer that it should be: {partiallyLoaded.LastWriteTime}, expected: {resumeLastWriteDate}");
                    partiallyLoaded = null;
                }

                // Looks like file is partially downloaded, but let’s ensure link still leads to the same content
                actualFootprint = GetFootprint(information, client.ResponseHeaders);
                if (partiallyLoaded != null && resumePreviousFootprint != actualFootprint) {
                    Logging.Warning($"Footprints don’t match: {resumePreviousFootprint}≠{actualFootprint}");
                    partiallyLoaded = null;
                }

                // Let’s check where to load data, which is potentially the most actual data at this point
                var destination = getPreferredDestination(Url, information);
                selectedDestination = destination.Filename;
                if (partiallyLoaded != null && (!destination.CanResumeDownload || !FileUtils.ArePathsEqual(selectedDestination, resumeDestination))) {
                    Logging.Warning($"Different destination chosen: {selectedDestination} (before: {resumeDestination})");
                    partiallyLoaded = null;
                }

                // TODO: Check that header?

                // Where to write?
                // ReSharper disable once MergeConditionalExpression
                filename = partiallyLoaded != null ? partiallyLoaded.FullName : FileUtils.EnsureUnique(true, destination.Filename);
                reportDestination?.Invoke(filename);

                // Set cancellation token
                cancellation.Register(o => client.CancelAsync(), null);

                // Open write stream
                if (partiallyLoaded != null) {
                    var rangeFrom = partiallyLoaded.Length;
                    using (client.SetRange(new Tuple<long, long>(rangeFrom, -1))) {
                        Logging.Warning($"Trying to resume download from {rangeFrom} bytes…");

                        remoteData.Dispose();
                        remoteData = await client.OpenReadTaskAsync(Url);
                        cancellation.ThrowIfCancellationRequested();
                        client.LogResponseHeaders();

                        // It’s unknown if resume is supported or not at this point
                        if (resumeSupported == null) {
                            var bytes = new byte[16];
                            var firstBytes = await remoteData.ReadAsync(bytes, 0, bytes.Length);
                            cancellation.ThrowIfCancellationRequested();

                            if (CouldBeBeginningOfAFile(bytes)) {
                                using (var file = File.OpenWrite(filename)) {
                                    Logging.Warning("File beginning found, restart download");
                                    file.Write(bytes, 0, firstBytes);
                                    await CopyToAsync(remoteData, file, checkIfPaused, progress, cancellation);
                                    cancellation.ThrowIfCancellationRequested();
                                }

                                Logging.Write("Download finished");
                                return filename;
                            }

                            rangeFrom += firstBytes;
                        }

                        using (var file = new FileStream(filename, FileMode.Append, FileAccess.Write)) {
                            await CopyToAsync(remoteData, file, checkIfPaused, new Progress<long>(v => {
                                progress?.Report(v + rangeFrom);
                            }), cancellation);
                            cancellation.ThrowIfCancellationRequested();
                        }
                    }
                } else {
                    if (headRequest) {
                        Logging.Warning("Re-open request to be GET");
                        remoteData.Dispose();
                        remoteData = await client.OpenReadTaskAsync(Url);
                    }

                    using (var file = File.OpenWrite(filename)) {
                        Logging.Debug("Downloading the whole file…");
                        await CopyToAsync(remoteData, file, checkIfPaused, progress, cancellation);
                        cancellation.ThrowIfCancellationRequested();
                    }
                }

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

        private async Task CopyToAsync(Stream input, Stream output, Func<bool> pauseCallback, IProgress<long> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var buffer = new byte[BufferSize];
            var totalPassed = 0L;

            int read;
            while ((read = await input.ReadAsync(buffer, 0, pauseCallback?.Invoke() == true ? 1 : buffer.Length, cancellation).ConfigureAwait(false)) > 0) {
                cancellation.ThrowIfCancellationRequested();

                await output.WriteAsync(buffer, 0, read, cancellation);
                cancellation.ThrowIfCancellationRequested();

                totalPassed += read;
                progress?.Report(totalPassed);

                if (pauseCallback?.Invoke() == true) {
                    await Task.Delay(50, cancellation);
                }

#if DEBUG
                await Task.Delay(10, cancellation);
#endif
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