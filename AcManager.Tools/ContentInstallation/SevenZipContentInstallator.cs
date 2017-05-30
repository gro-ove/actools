using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    internal class SevenZipContentInstallator : ContentInstallatorBase {
        public static readonly string PluginId = "7Zip";

        public static async Task<IAdditionalContentInstallator> Create(string filename, ContentInstallationParams installationParams, CancellationToken c) {
            var result = new SevenZipContentInstallator(filename, installationParams);
            await result.TestPasswordAsync(c);
            return result;
        }

        private readonly string _filename;
        private readonly string _executable;

        private SevenZipContentInstallator(string filename, ContentInstallationParams installationParams) : base(installationParams) {
            _filename = filename;

            var plugin = PluginsManager.Instance.GetById(PluginId);
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

        private class SevenZipBinaryResult : SevenZipResult {
            public readonly byte[] Out;

            public SevenZipBinaryResult(byte[] data, string[] error) : base(error) {
                Out = data;
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
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
        }

        [ItemCanBeNull]
        private async Task<SevenZipTextResult> Execute(IEnumerable<string> args, string directory, CancellationToken c){
            using (var process = Run(args, directory)){
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

        [ItemCanBeNull]
        private async Task<SevenZipBinaryResult> ExecuteBinary(IEnumerable<string> args, string directory, CancellationToken c){
            using (var memory = new MemoryStream()) {
                var result = await ExecuteBinary(args, directory, s => s.CopyToAsync(memory), c);
                return result == null ? null : new SevenZipBinaryResult(memory.ToArray(), result.Error);
            }
        }
        #endregion

        #region Parsing
        private class SevenZipEntry {
            public string Key;
            public long Size;
        }

        private static readonly Regex RegexParseLine = new Regex(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d (\S{5})\s+(\d+)\s{1,12}\d*\s+(.+)$", RegexOptions.Compiled);

        private static SevenZipEntry ParseListOfFiles_Line(string line){
            if (line.Length < 20) return null;

            var m = RegexParseLine.Match(line);
            return m.Success ? m.Groups[1].Value.StartsWith("D") ? null : new SevenZipEntry {
                Key = m.Groups[3].Value,
                Size = FlexibleParser.TryParseLong(m.Groups[2].Value) ?? 0L
            } : null;
        }

        private static IEnumerable<SevenZipEntry> ParseListOfFiles(string[] o){
            return o.Select(ParseListOfFiles_Line).NonNull();
        }

        private static void CheckForErrors(string[] o) {
            Logging.Debug("Errors: " + o.JoinToString("\n"));
            foreach (var error in o.Where(x => x.StartsWith("ERROR:"))) {
                if (error.Contains("Wrong password")) {
                    Logging.Debug("Password is invalid");
                    throw new CryptographicException("Password is incorrect");
                }
            }
        }
        #endregion

        #region 7-zip methods
        [ItemCanBeNull]
        private async Task<List<SevenZipEntry>> ListFiles(CancellationToken c) {
            var o = await Execute(new[] {
                "l", $"-p{Password}", "--",
                Path.GetFileName(_filename)
            }, Path.GetDirectoryName(_filename), c);
            if (o == null) return null;

            CheckForErrors(o.Error);
            return ParseListOfFiles(o.Out).ToList();
        }

        [ItemCanBeNull]
        private async Task<byte[]> GetFiles([NotNull] IEnumerable<string> keys, CancellationToken c) {
            var o = await ExecuteBinary(new[] {
                "e", "-so", $"-p{Password}", "--",
                Path.GetFileName(_filename)
            }.Concat(keys), Path.GetDirectoryName(_filename), c);
            if (o == null) return null;

            CheckForErrors(o.Error);
            return o.Out;
        }

        private async Task GetFiles([NotNull] IEnumerable<string> keys, Func<Stream, Task> streamCallback, CancellationToken c) {
            var o = await ExecuteBinary(new[] {
                "e", "-so", $"-p{Password}", "--",
                Path.GetFileName(_filename)
            }.Concat(keys), Path.GetDirectoryName(_filename), streamCallback, c);
            if (o == null) return;

            CheckForErrors(o.Error);
        }

        private async Task GetFiles(Func<Stream, Task> streamCallback, CancellationToken c) {
            var o = await ExecuteBinary(new[] {
                "e", "-so", $"-p{Password}", "--",
                Path.GetFileName(_filename)
            }, Path.GetDirectoryName(_filename), streamCallback, c);
            if (o == null) return;

            CheckForErrors(o.Error);
        }
        #endregion

        private bool _passwordCorrect;

        private async Task TestPasswordAsync(CancellationToken c) {
            Logging.Debug(_filename);

            try {
                var list = await ListFiles(c);

                // TODO: for solid archive, load first entry, otherwise, the smallest one
                var first = list?.FirstOrDefault();
                if (first == null) return;

                await GetFiles(new[]{ first.Key }, s => Task.Delay(0), c);
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

        private class SevenZipFileInfo : IFileInfo {
            private readonly SevenZipEntry _archiveEntry;
            private readonly Func<string, byte[]> _reader;

            public SevenZipFileInfo(SevenZipEntry archiveEntry, Func<string, byte[]> reader = null) {
                _archiveEntry = archiveEntry;
                _reader = reader;
            }

            public string Key => _archiveEntry.Key.Replace('/', '\\');

            public long Size => _archiveEntry.Size;

            public async Task<byte[]> ReadAsync() {
                if (_reader == null) throw new NotSupportedException();
                return await Task.Run(() => _reader(_archiveEntry.Key)).ConfigureAwait(false);
            }

            public Task CopyToAsync(string destination) {
                throw new NotSupportedException();
            }
        }

        protected override async Task<IEnumerable<IFileInfo>> GetFileEntriesAsync(CancellationToken cancellation) {
            return (await ListFiles(cancellation))?.Select(x => new SevenZipFileInfo(x, ReadData));
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

        protected override async Task LoadMissingContents(CancellationToken cancellation) {
            if (_askedData == null) return;

            if (_preloadedData == null) {
                _preloadedData = new Dictionary<string, byte[]>();
            }

            var list = (await ListFiles(cancellation))?.Where(x => _askedData.Contains(x.Key)).ToList();
            if (list == null) return;

            await GetFiles(list.Select(x => x.Key), async s => {
                foreach (var l in list) {
                    var buffer = new byte[l.Size];
                    Array.Resize(ref buffer, await s.ReadAsync(buffer, 0, buffer.Length));
                    _preloadedData[l.Key] = buffer;
                }
            }, cancellation).ConfigureAwait(false);
        }

        protected override async Task CopyFileEntries(CopyCallback callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var filtered = (await ListFiles(cancellation))?.Select(x => {
                var destination = callback(new SevenZipFileInfo(x));
                return destination == null ? null : Tuple.Create(x.Key, x.Size, destination);
            }).NonNull().ToList();
            if (filtered == null) return;

            await GetFiles(filtered.Select(x => x.Item1), async s => {
                for (var i = 0; i < filtered.Count; i++) {
                    var entry = filtered[i];

                    Logging.Debug(entry.Item1 + "→" + entry.Item3);

                    FileUtils.EnsureFileDirectoryExists(entry.Item3);
                    progress?.Report(Path.GetFileName(entry.Item3), i, filtered.Count);

                    using (var write = File.Create(entry.Item3)) {
                        await s.CopyToAsync(write, entry.Item2);
                        if (cancellation.IsCancellationRequested) return;
                    }
                }
            }, cancellation);
        }
    }
}