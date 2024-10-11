using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using HtmlAgilityPack;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Loaders {
    public class DirectLoader : ILoader {
        public static bool OptionUseHeadRequests = false;
        
        public int BufferSize { get; set; } = 65536;

        private readonly string _keyDestination;
        private readonly string _keyPartiallyLoadedFilename;
        private readonly string _keyFootprint;
        private readonly string _keyLastWriteDate;
        protected string Url;

        public ILoader Parent { get; set; }
        public long? TotalSize { get; protected set; }
        public string Version { get; protected set; }
        public string FileName { get; protected set; }

        private static string GetFileNameFromUrl(string url) {
            var p = url.LastIndexOf('/');
            if (p != -1) {
                var fileName = url.Substring(p + 1);
                p = fileName.IndexOfAny(new[] { '?', '#' });
                if (p != -1) {
                    fileName = fileName.Substring(0, p);
                }
                if (Regex.IsMatch(fileName, @"^[\w()[\] ~.-]+\.\w+$")) {
                    return fileName;
                }
            }
            return null;
        }

        public DirectLoader(string url) {
            _keyDestination = ".DirectLoader.Destination:" + url;
            _keyPartiallyLoadedFilename = ".DirectLoader.PartiallyLoadedFilename:" + url;
            _keyFootprint = ".DirectLoader.Footprint:" + url;
            _keyLastWriteDate = ".DirectLoader.LastWriteDate:" + url;

            if (GetType() == typeof(DirectLoader)) {
                FileName = GetFileNameFromUrl(url);
            }

            Url = url;
        }

        public virtual Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            return Task.FromResult(true);
        }

        public Task<string> DownloadAsync(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination = null, Func<bool> checkIfPaused = null,
                IProgress<long> progress = null, CancellationToken cancellation = default) {
            return DownloadAsyncInner(client, getPreferredDestination, reportDestination, checkIfPaused, progress, cancellation);
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
            if (bytes.StartsWith((byte)'R', (byte)'a', (byte)'r', (byte)'!', 0x1A, 0x07, 0x01, 0x00)) return true;

            // RAR archive v1.5+
            if (bytes.StartsWith((byte)'R', (byte)'a', (byte)'r', (byte)'!', 0x1A, 0x07, 0x00)) return true;

            // 7-Zip archive
            if (bytes.StartsWith((byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C)) return true;

            // GZIP
            // if (bytes.StartsWith(new byte[] { 0x1F, 0x8B })) return true;

            // ZIP
            if (bytes.StartsWith(0x50, 0x4B, 0x03, 0x04)) return true;

            // TAR archive
            if (bytes.StartsWith(0x75, 0x73, 0x74, 0x61, 0x72, 0x00, 0x30, 0x30)
                    || bytes.StartsWith(0x75, 0x73, 0x74, 0x61, 0x72, 0x20, 0x20, 0x00)) return true;

            return false;
        }

        protected virtual string TryToFixHtmlWebpage(HtmlDocument doc) {
            return null;
        }

        private class HeadedStream {
            public Stream Data;
            public bool FromHead;
        }

        private static async Task<HeadedStream> TryHeadFirst(CookieAwareWebClient client, bool useHead, string targetUrl) {
            if (useHead && OptionUseHeadRequests) {
                var domain = targetUrl.GetDomainNameFromUrl();
                if (CacheStorage.Get($".hdlssdmn.{domain}", 0) != 1) {
                    try {
                        using (client.SetMethod(@"HEAD")) {
                            return new HeadedStream { Data = await client.OpenReadTaskAsync(targetUrl).ConfigureAwait(false), FromHead = true };
                        }
                    } catch (Exception e) when (e.IsWebException()) {
                        Logging.Debug($"Headless domain: {domain}, {e}");
                        CacheStorage.Set($".hdlssdmn.{domain}", 1);
                    }
                }
            }
            return new HeadedStream { Data = await client.OpenReadTaskAsync(targetUrl).ConfigureAwait(false), FromHead = false };
        }

        private async Task<string> DownloadResumeSupportAsync([NotNull] CookieAwareWebClient client,
                [NotNull] FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                [CanBeNull] FlexibleLoaderReportDestinationCallback reportDestination, [CanBeNull] Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            // Common variables
            string filename = null, selectedDestination = null, actualFootprint = null;
            HeadedStream remoteData = null;
            string loadedData = null;

            var resumeSupported = ResumeSupported;

            try {
                // Read resume-related data and remove it to avoid conflicts
                var resumeDestination = CacheStorage.Get<string>(_keyDestination);
                var resumePartiallyLoadedFilename = CacheStorage.Get<string>(_keyPartiallyLoadedFilename);
                var resumeLastWriteDate = CacheStorage.Get<DateTime?>(_keyLastWriteDate);
                var resumePreviousFootprint = CacheStorage.Get<string>(_keyFootprint);
                ClearResumeData();

                // Collect known information for destination callback
                var information = FlexibleLoaderMetaInformation.FromLoader(this);

                // Opening stream to read…
                {
                    var targetUrl = Url;
                    using (client.SetAutoRedirect(false)) {
                        for (var i = 0;; ++i) {
                            remoteData = await TryHeadFirst(client, HeadRequestSupported && resumeDestination != null, targetUrl);
                            cancellation.ThrowIfCancellationRequested();

                            if (string.IsNullOrWhiteSpace(client.ResponseLocation)) break;
                            remoteData.Data.Dispose();

                            if (targetUrl == client.ResponseLocation) throw new Exception("Looping redirect");
                            if (i == 20) throw new Exception("Too many redirects");

                            Logging.Debug($"Redirect from {targetUrl} to {client.ResponseLocation}");
                            
                            var supported = await FlexibleLoader.IsSupportedAsync(client.ResponseLocation, cancellation);
                            cancellation.ThrowIfCancellationRequested();

                            if (supported) {
                                throw new RestartLoadingException(client.ResponseLocation);
                            }
                            
                            targetUrl = client.ResponseLocation;
                            if (client.TryGetFileName(out var fileName) && fileName != FileName) {
                                FileName = information.FileName = fileName;
                                progress?.Report(long.MinValue);
                                Logging.Debug($"[DLOADER] Updating file name: {fileName}");
                            } else {
                                fileName = GetFileNameFromUrl(targetUrl);
                                if (fileName != null && fileName != FileName) {
                                    FileName = fileName;
                                    progress?.Report(long.MinValue);
                                    Logging.Debug($"[DLOADER] Updating file name to file name from URL: {fileName}");
                                }
                            }
                        }
                    }
                }

                // Maybe we’ll be lucky enough to load the most accurate data
                if (client.ResponseHeaders != null) {
                    if (long.TryParse(client.ResponseHeaders[HttpResponseHeader.ContentLength] ?? "",
                            NumberStyles.Any, CultureInfo.InvariantCulture, out var length)) {
                        if (client.ResponseHeaders[HttpResponseHeader.ContentType]?.StartsWith("text/html") == true
                                && length < 256 * 1024) {
                            Logging.Debug("HTML webpage detected, checking for redirect");
                            if (remoteData.FromHead) {
                                Logging.Warning("Re-open request to be GET");
                                remoteData.Data.Dispose();
                                remoteData.Data = await client.OpenReadTaskAsync(Url);
                            }

                            loadedData = remoteData.Data.ReadAsStringAndDispose();
                            remoteData.Data = null;
                            var doc = new HtmlDocument();
                            doc.LoadHtml(loadedData);

                            var link = doc.DocumentNode.SelectSingleNode(@"//meta[contains(@http-equiv, 'refresh')]")?.Attributes[@"content"]?.Value;
                            if (link == null) {
                                Logging.Warning("Redirect is missing: " + loadedData);

                                var fixedUrl = TryToFixHtmlWebpage(doc);
                                if (fixedUrl != null) {
                                    loadedData = null;
                                    remoteData.Data = await client.OpenReadTaskAsync(fixedUrl);
                                    /*var innerLoader =  await FlexibleLoader.CreateLoaderAsync(fixedUrl, this, cancellation);
                                    if (innerLoader != null) {
                                        return await innerLoader.DownloadAsync(client, getPreferredDestination, reportDestination,
                                                checkIfPaused, progress, cancellation);
                                    }*/
                                }
                            } else {
                                var url = Regex.Match(link, @"\bhttp.+");
                                if (url.Success) {
                                    Logging.Debug("Redirect to " + url.Value);
                                    var innerLoader = await FlexibleLoader.CreateLoaderAsync(url.Value, this, cancellation);
                                    if (innerLoader != null) {
                                        return await innerLoader.DownloadAsync(client, getPreferredDestination, reportDestination,
                                                checkIfPaused, progress, cancellation);
                                    }
                                }
                            }
                        }

                        TotalSize = information.TotalSize = length;
                    }

                    if (client.TryGetFileName(out var fileName)) {
                        FileName = information.FileName = fileName;
                        Logging.Debug($"[DLOADER] Updating file name (final): {fileName}");
                    }

                    // For example, Google Drive responds with “none” and yet allows to download file partially,
                    // so this header will only be checked if value is not defined.
                    if (resumeSupported == null && loadedData == null) {
                        var accept = client.ResponseHeaders[HttpResponseHeader.AcceptRanges] ?? "";
                        if (accept.Contains("bytes")) {
                            resumeSupported = true;
                        } else if (accept.Contains("none")) {
                            resumeSupported = false;
                        }
                    }

                    // client.LogResponseHeaders();
                }

                // Was the file partially loaded before?
                var partiallyLoaded = ResumeSupported != false && resumePartiallyLoadedFilename != null
                        ? new FileInfo(FileUtils.EnsureFilenameIsValid(resumePartiallyLoadedFilename, true)) : null;
                if (partiallyLoaded != null) {
                    Logging.Warning("Not finished: " + partiallyLoaded);
                }

                // Does it still exist
                if (partiallyLoaded?.Exists != true) {
                    Logging.Warning($"Partially downloaded file “{partiallyLoaded?.FullName ?? "<null>"}” does not exist");
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
                if (partiallyLoaded != null && loadedData == null) {
                    var rangeFrom = partiallyLoaded.Length;
                    using (client.SetRange(new Tuple<long, long>(rangeFrom, -1))) {
                        Logging.Warning($"Trying to resume download from {rangeFrom} bytes…");

                        remoteData.Data.Dispose();
                        remoteData.Data = await client.OpenReadTaskAsync(Url);
                        cancellation.ThrowIfCancellationRequested();
                        // client.LogResponseHeaders();

                        // It’s unknown if resume is supported or not at this point
                        if (resumeSupported == null) {
                            var bytes = new byte[16];
                            var firstBytes = await remoteData.Data.ReadAsync(bytes, 0, bytes.Length);
                            cancellation.ThrowIfCancellationRequested();

                            if (CouldBeBeginningOfAFile(bytes)) {
                                using (var file = File.Create(filename)) {
                                    Logging.Warning("File beginning found, restart download");
                                    file.Write(bytes, 0, firstBytes);
                                    await CopyToAsync(remoteData.Data, file, checkIfPaused, progress, cancellation);
                                    cancellation.ThrowIfCancellationRequested();
                                }

                                Logging.Write("Download finished");
                                return filename;
                            }

                            rangeFrom += firstBytes;
                        }

                        using (var file = new FileStream(filename, FileMode.Append, FileAccess.Write)) {
                            await CopyToAsync(remoteData.Data, file, checkIfPaused, new Progress<long>(v => { progress?.Report(v + rangeFrom); }), cancellation);
                            cancellation.ThrowIfCancellationRequested();
                        }
                    }
                } else if (loadedData != null) {
                    File.WriteAllText(filename, loadedData);
                } else {
                    if (remoteData.FromHead) {
                        Logging.Warning("Re-open request to be GET");
                        remoteData.Data.Dispose();
                        remoteData.Data = await client.OpenReadTaskAsync(Url);
                    }

                    using (var file = File.Create(filename)) {
                        Logging.Debug("Downloading the whole file…");
                        await CopyToAsync(remoteData.Data, file, checkIfPaused, progress, cancellation);
                        cancellation.ThrowIfCancellationRequested();
                    }
                }

                Logging.Write("Download finished");
                return filename;
            } catch (Exception e) when (e is WebException || e.IsCancelled()) {
                var download = filename == null ? null : new FileInfo(filename);
                if (download?.Exists == true && download.Length > 0) {
                    Logging.Write("Download is interrupted! Saving details to resume later…");
                    CacheStorage.Set(_keyDestination, selectedDestination);
                    CacheStorage.Set(_keyPartiallyLoadedFilename, filename);
                    CacheStorage.Set(_keyFootprint, actualFootprint);
                    CacheStorage.Set(_keyLastWriteDate, download.LastWriteTime);
                } else {
                    Logging.Write("Download is interrupted, but nothing has been downloaded, can’t resume later");
                    ClearResumeData();
                }

                throw;
            } finally {
                remoteData?.Data?.Dispose();
            }
        }

        private async Task CopyToAsync(Stream input, Stream output, Func<bool> pauseCallback, IProgress<long> progress = null,
                CancellationToken cancellation = default) {
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
                // await Task.Delay(10, cancellation);
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