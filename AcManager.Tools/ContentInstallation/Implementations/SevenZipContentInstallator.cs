using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Implementations {
    public class SevenZipContentInstallator : ContentInstallatorBase {
        public static async Task<IAdditionalContentInstallator> Create([NotNull] string filename, [NotNull] ContentInstallationParams installationParams,
                CancellationToken c) {
            var result = new SevenZipContentInstallator(filename, installationParams);
            await result.TestPasswordAsync(c);
            return result;
        }

        private readonly string _filename;
        private readonly string _executable;

        private SevenZipContentInstallator([NotNull] string filename, [NotNull] ContentInstallationParams installationParams) : base(installationParams) {
            _filename = filename;

            var plugin = PluginsManager.Instance.GetById(KnownPlugins.SevenZip);
            if (plugin?.IsReady != true) throw new Exception("Plugin 7-Zip is required");

            _executable = plugin.GetFilename("7z.exe");
            if (!File.Exists(_executable)) throw new FileNotFoundException("7-Zip executable not found", filename);
        }

        #region Processes
        private static string[] Split(string o) {
            var arr = o.Split('\n');
            for (var i = 0; i < arr.Length; i++) {
                arr[i] = arr[i].Trim();
            }
            return arr;
        }

        private class SevenZipResult {
            public readonly string[] Error;

            public SevenZipResult(string[] error) {
                Error = error;
            }
        }

        private class SevenZipTextResult : SevenZipResult {
            public readonly string[] Out;

            public SevenZipTextResult(string[] output, string[] error) : base(error) {
                Out = output;
            }
        }

        [NotNull]
        private Process Run(IEnumerable<string> args, string directory) {
            var argsLine = args.Select(ProcessExtension.GetQuotedArgument).JoinToString(" ");
            Logging.Debug(argsLine);
            return new Process {
                StartInfo = {
                    FileName = _executable,
                    WorkingDirectory = directory,
                    Arguments = argsLine,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
        }

        private class SevenZipList : IDisposable {
            private readonly string _filename;

            public string Value => $@"@{_filename}";

            public SevenZipList(IEnumerable<string> items) {
                _filename = Path.GetTempFileName();
                File.WriteAllLines(_filename, items, Encoding.UTF8);
            }

            public void Dispose() {
                try {
                    // File.Delete(_filename);
                } catch {
                    // ignored
                }
            }
        }

        [ItemCanBeNull]
        private async Task<SevenZipTextResult> Execute(IEnumerable<string> args, string directory, CancellationToken c) {
            using (var process = Run(args, directory)) {
                var o = new StringBuilder();
                var e = new StringBuilder();
                process.OutputDataReceived += (sender, eventArgs) => o.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => e.AppendLine(eventArgs.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.StandardInput.Close();

                await process.WaitForExitAsync(c).ConfigureAwait(false);
                return c.IsCancellationRequested ? null : new SevenZipTextResult(Split(o.ToString()), Split(e.ToString()));
            }
        }

        [ItemCanBeNull]
        private async Task<SevenZipResult> ExecuteBinary(IEnumerable<string> args, string directory, Func<Stream, Task> streamCallback,
                CancellationToken c) {
            using (var process = Run(args, directory)) {
                process.Start();
                process.StandardInput.Close();

                await streamCallback(process.StandardOutput.BaseStream);
                process.StandardOutput.BaseStream.Close();

                var error = process.StandardError.BaseStream.ReadAsString();

                await process.WaitForExitAsync(c).ConfigureAwait(false);
                return c.IsCancellationRequested ? null : new SevenZipResult(Split(error));
            }
        }
        #endregion

        #region Parsing
        private class SevenZipEntry {
            public string Key;
            public bool IsDirectory;
            public long Size;
        }

        private static readonly Regex RegexParseLine = new Regex(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d (\S{5})\s+(\d+)\s{1,12}\d*\s+(.+)$", RegexOptions.Compiled);

        private static SevenZipEntry ParseListOfFiles_Line(string line) {
            if (line.Length < 20) return null;

            var m = RegexParseLine.Match(line);
            if (!m.Success) return null;

            var key = m.Groups[3].Value;
            return new SevenZipEntry {
                Key = key.Trim(),
                IsDirectory = m.Groups[1].Value.StartsWith("D"),
                Size = FlexibleParser.TryParseLong(m.Groups[2].Value) ?? 0L
            };
        }

        private static IEnumerable<SevenZipEntry> ParseListOfFilesOrDirectories(string[] o) {
            return o.Select(ParseListOfFiles_Line).NonNull();
        }

        private static void CheckForErrors(string[] o) {
            foreach (var error in o.Where(x => x.StartsWith("ERROR:"))) {
                if (error.Contains("Wrong password")) {
                    Logging.Debug("Password is invalid");
                    throw new CryptographicException("Password is incorrect");
                }
            }
        }
        #endregion

        #region 7-zip methods
        private List<SevenZipEntry> _cachedList;
        private bool _solidArchive;

        [ItemCanBeNull]
        private async Task<List<SevenZipEntry>> ListFilesOrDirectories(CancellationToken c) {
            if (_cachedList != null) {
                return _cachedList;
            }

            var o = await Execute(new[] {
                "l", $"-p{Password}", "-sccUTF-8", "-scsUTF-8", "--",
                Path.GetFileName(_filename)
            }, Path.GetDirectoryName(_filename), c);
            if (o == null) return null;

            CheckForErrors(o.Error);

            var solidLine = o.Out.FirstOrDefault(x => x.StartsWith("Solid = "));
            if (solidLine != null) {
                _solidArchive = solidLine == "Solid = +";
            }

            _cachedList = ParseListOfFilesOrDirectories(o.Out).ToList();
            return _cachedList;
        }

        private async Task GetFiles([NotNull] IEnumerable<string> keys, Func<Stream, Task> streamCallback, CancellationToken c) {
            using (var list = new SevenZipList(keys)) {
                var o = await ExecuteBinary(new[] {
                    "e", "-so", $"-p{Password}", "-sccUTF-8", "-scsUTF-8", $"-i{list.Value}", "--",
                    Path.GetFileName(_filename)
                }, Path.GetDirectoryName(_filename), streamCallback, c);
                if (o == null) return;
                CheckForErrors(o.Error);
            }
        }
        #endregion

        private bool _passwordCorrect;

        private async Task TestPasswordAsync(CancellationToken c) {
            Logging.Debug(_filename);

            try {
                var list = (await ListFilesOrDirectories(c))?.Where(x => !x.IsDirectory);
                if (list == null) return;

                SevenZipEntry testFile;
                if (_solidArchive == false) {
                    Logging.Debug("Archive is not solid, testing password on the smallest file…");
                    testFile = list.MinEntryOrDefault(x => x.Size);
                } else {
                    Logging.Debug("Archive is solid, testing password on the first file…");
                    testFile = list.FirstOrDefault();
                }

                if (testFile == null) return;

                await GetFiles(new[] { testFile.Key }, s => Task.Delay(0), c);
                _passwordCorrect = true;
            } catch (CryptographicException) {
                IsPasswordRequired = true;
                _passwordCorrect = false;
            }
        }

        public override Task TrySetPasswordAsync(string password, CancellationToken cancellation) {
            Password = password;
            return TestPasswordAsync(cancellation);
        }

        public override bool IsPasswordCorrect => !IsPasswordRequired || _passwordCorrect;

        protected override string GetBaseId() {
            var id = Path.GetFileNameWithoutExtension(_filename)?.ToLower();
            return AcStringValues.IsAppropriateId(id) ? id : null;
        }

        protected override string GetBaseName() {
            return Path.GetFileNameWithoutExtension(_filename);
        }

        private class SevenZipDirectoryInfo : IDirectoryInfo {
            private readonly SevenZipEntry _archiveEntry;

            public SevenZipDirectoryInfo(SevenZipEntry archiveEntry) {
                _archiveEntry = archiveEntry;
            }

            public string Key => _archiveEntry.Key.Replace('/', '\\');
        }

        private class SevenZipFileInfo : IFileInfo {
            private readonly SevenZipEntry _archiveEntry;
            private readonly Func<string, byte[]> _reader;
            private readonly Func<string, bool> _tester;

            public SevenZipFileInfo(SevenZipEntry archiveEntry, Func<string, byte[]> reader, Func<string, bool> tester) {
                _archiveEntry = archiveEntry;
                _reader = reader;
                _tester = tester;
            }

            public string Key => _archiveEntry.Key.Replace('/', '\\');
            public long Size => _archiveEntry.Size;

            public async Task<byte[]> ReadAsync() {
                if (_reader == null) throw new NotSupportedException();
                return await Task.Run(() => _reader(_archiveEntry.Key)).ConfigureAwait(false);
            }

            public bool IsAvailable() {
                return _tester?.Invoke(_archiveEntry.Key) == true;
            }

            public Task CopyToAsync(string destination) {
                throw new NotSupportedException();
            }
        }

        protected override async Task<IEnumerable<IFileOrDirectoryInfo>> GetFileEntriesAsync(CancellationToken cancellation) {
            return (await ListFilesOrDirectories(cancellation))?.Select(x => x.IsDirectory ?
                    (IFileOrDirectoryInfo)new SevenZipDirectoryInfo(x) : new SevenZipFileInfo(x, ReadData, CheckData));
        }

        private List<string> _askedData;
        private Dictionary<string, byte[]> _preloadedData;

        private byte[] ReadData(string key) {
            if (_preloadedData != null && _preloadedData.TryGetValue(key, out byte[] data)) {
                return data;
            }

            if (_askedData == null) {
                _askedData = new List<string> { key };
            } else {
                _askedData.Add(key);
            }

            return null;
        }

        private bool CheckData(string key) {
            if (_preloadedData != null && _preloadedData.ContainsKey(key)) {
                return true;
            }

            if (_askedData == null) {
                _askedData = new List<string> { key };
            } else {
                _askedData.Add(key);
            }

            return false;
        }

        protected override async Task LoadMissingContents(CancellationToken cancellation) {
            if (_askedData == null) return;

            if (_preloadedData == null) {
                _preloadedData = new Dictionary<string, byte[]>();
            }

            var list = (await ListFilesOrDirectories(cancellation))?.Where(x => !x.IsDirectory && _askedData.Contains(x.Key)).ToList();
            if (list == null) return;

            // for debugging in case of unexpected end
            var readInTotal = 0;
            var readList = new Dictionary<string, byte[]>();

            await GetFiles(list.Select(x => x.Key), async s => {
                foreach (var l in list) {
                    var buffer = new byte[l.Size];
                    var read = 0;
                    var waiting = 0;

                    while (read < l.Size) {
                        var local = await s.ReadAsync(buffer, read, buffer.Length - read);
                        read += local;
                        readInTotal += local;

                        if (read != l.Size && local == 0) {
                            if (waiting < 2) {
                                waiting++;
                                await Task.Delay(500);
                            } else {
                                Logging.Debug("Entries to read:\n" + list.Select(x => $"{x.Key} ({x.Size} bytes)"));
                                Logging.Debug($"Summary required: {list.Sum(x => x.Size)} bytes");
                                Logging.Debug($"Able to read: {readInTotal} bytes, {readList.Count} entries");
                                Logging.Debug($"Now trying to read: {l.Key}");

                                using (var archive = ZipFile.Open(FileUtils.EnsureUnique(FilesStorage.Instance.GetTemporaryFilename("Unexpected end.zip")),
                                        ZipArchiveMode.Create)) {
                                    foreach (var r in readList) {
                                        archive.AddBytes(r.Key, r.Value);
                                    }
                                    archive.AddBytes(l.Key, buffer, 0, read);
                                }

                                Logging.Debug("Dump as archive created, look for it in CM’s temporary directory");
                                throw new Exception("Unexpected end");
                            }
                        }
                    }

                    readList[l.Key] = buffer;
                    _preloadedData[l.Key] = buffer;
                }
            }, cancellation).ConfigureAwait(false);
        }

        protected override async Task CopyFileEntries(ICopyCallback callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var filtered = (await ListFilesOrDirectories(cancellation))?.Select(x => {
                var destination = x.IsDirectory ? callback.Directory(new SevenZipDirectoryInfo(x)) :
                        callback.File(new SevenZipFileInfo(x, null, null));
                return destination == null ? null : Tuple.Create(x.Key, x.IsDirectory, x.Size, destination);
            }).NonNull().ToList();
            if (filtered == null) return;

            await GetFiles(filtered.Where(x => !x.Item2).Select(x => x.Item1), async s => {
                for (var i = 0; i < filtered.Count; i++) {
                    var entry = filtered[i];
                    // Logging.Debug(entry.Item1 + "→" + entry.Item4);

                    if (entry.Item2) {
                        FileUtils.EnsureDirectoryExists(entry.Item4);
                    } else if (entry.Item4 != null) {
                        FileUtils.EnsureFileDirectoryExists(entry.Item4);
                        progress?.Report(Path.GetFileName(entry.Item4), i, filtered.Count);

                        Stream stream = null;
                        for (var j = 0; j < 3 && stream == null; j++) {
                            try {
                                stream = File.Create(entry.Item4);
                                break;
                            } catch (IOException e) {
                                Logging.Warning(e);
                                await Task.Delay(300);
                            }
                        }

                        if (stream == null) {
                            Logging.Warning("Failed to create a destination file");
                        }

                        using (var write = stream ?? new MemoryStream()) {
                            await s.CopyToAsync(entry.Item3, write, 30);
                            if (cancellation.IsCancellationRequested) return;
                        }

                        if (stream != null) {
                            var written = new FileInfo(entry.Item4).Length;
                            // Logging.Debug("Written bytes: " + written);
                            if (written != entry.Item3) {
                                throw new Exception($"Sizes of {entry.Item1} don’t match: expected {entry.Item3}, but written {written}");
                            }
                        }
                    }
                }
            }, cancellation);

            foreach (var entry in filtered.Where(x => x.Item2)) {
                FileUtils.EnsureDirectoryExists(entry.Item4);
            }
        }
    }
}