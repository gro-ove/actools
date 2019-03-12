using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using xxHashSharp;

namespace AcManager.Tools.Data {
    public class PatchVersionInfo : NotifyPropertyChanged {
        [JsonProperty("changelog")]
        public string Changelog { get; set; }

        [JsonProperty("tags"), CanBeNull]
        public string[] Tags { get; set; }

        [JsonProperty("url"), CanBeNull]
        public string Url { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("chunk")]
        public string ChunkVersion { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("chunkSize")]
        public long ChunkSize { get; set; }

        [JsonProperty("build")]
        public int Build { get; set; }

        public long TotalSize => Size + ChunkSize;

        public string DisplaySize => Size.ToReadableSize();
        public string DisplayChunkSize => ChunkSize.ToReadableSize();
        public string DisplayTotalSize => TotalSize.ToReadableSize();

        public bool IsRecommended => Tags?.Contains(@"recommended") == true;
        public bool IsTested => Tags?.Contains(@"tested") == true;

        private bool? _isDownloaded;

        public bool IsDownloaded {
            get {
                if (_isDownloaded == null) {
                    _isDownloaded = CmApiProvider.HasPatchCached(Version);
                }
                return _isDownloaded ?? false;
            }
            set => Apply(value, ref _isDownloaded);
        }

        public bool AvailableToDownload => Size > 0;

        private bool _isInstalled;

        public bool IsInstalled {
            get => _isInstalled;
            set => Apply(value, ref _isInstalled);
        }

        private static bool _installing;

        private class InstalledFile {
            public FileInfo File;
            public string Checksum;
            public long Length;
            public DateTime LastWrite;
            public bool ToRecycle = true;
        }

        private class InstallStage {
            private readonly string _stageName;
            private readonly string _modsDirectory;
            private readonly string[] _forcedFiles;

            [CanBeNull]
            private StreamWriter _installationLog;

            private readonly string _patchLocation;

            private readonly List<string> _directoriesToRemove = new List<string>();
            private readonly List<InstalledFile> _filesToRemove = new List<InstalledFile>();

            public InstallStage(string stageName, string modsDirectory, string[] forcedFiles) {
                _stageName = stageName;
                _modsDirectory = modsDirectory;
                _forcedFiles = forcedFiles;
                _patchLocation = PatchHelper.GetRootDirectory();
            }

            public void AddDirectoryToRemove(string value) {
                _directoriesToRemove.Add(value);
            }

            public void AddFileToRemove(InstalledFile file) {
                _filesToRemove.Add(file);
            }

            public void Run([NotNull] byte[] data, [CanBeNull] StreamWriter log, IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
                _installationLog = log;

                progress.Report($"Recycling modified {_stageName} files", 0.01);
                var toRecycle = _filesToRemove.Where(x => x.File.Exists
                        && ((x.File.LastWriteTime - x.LastWrite).TotalSeconds > 2d || x.Length != x.File.Length)).ToList();
                Logging.Debug(
                        $"Changed files to recycle: {(toRecycle.Count == 0 ? @"none" : toRecycle.Select(x => $"\n• {x.File.FullName}").JoinToString(@";"))}");

                FileUtils.Recycle(toRecycle.Select(x => x.File.FullName).ToArray());
                _filesToRemove.RemoveAll(x => toRecycle.Contains(x));

                if (data.Length > 0) {
                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        if (_forcedFiles.Length > 0) {
                            var split = progress.Split(0.2);
                            ProcessForced(split.Item1, cancellation, archive);
                            if (cancellation.IsCancellationRequested) return;
                            progress = split.Item2;
                        }

                        var subProgress = progress.Subrange(0.0, 0.9);
                        for (var i = 0; i < archive.Entries.Count; i++) {
                            ProcessZipEntry(archive.Entries[i], i, archive.Entries.Count, subProgress);
                        }
                    }
                }

                progress.Report($"Recycling old {_stageName} files", 0.94);
                var oldFiles = _filesToRemove.Where(x => x.ToRecycle).Select(x => x.File.FullName).ToList();
                Logging.Debug($"Old files to remove: {(oldFiles.Count == 0 ? @"none" : oldFiles.Select(x => $"\n• {x}").JoinToString(@";"))}");
                oldFiles.ForEach(x => FileUtils.TryToDelete(x));

                progress.Report($"Deleting empty {_stageName} directories", 0.97);
                var emptyDirectories = _directoriesToRemove.Where(x => FileUtils.IsDirectoryEmpty(x, false)).ToList();
                Logging.Debug(
                        $"Empty directories to remove: {(emptyDirectories.Count == 0 ? @"none" : emptyDirectories.Select(x => $"\n• {x}").JoinToString(@";"))}");
                emptyDirectories.ForEach(x => FileUtils.TryToDeleteDirectory(x));
            }

            private void ProcessZipEntry(ZipArchiveEntry entry, int i, int count, IProgress<AsyncProgressEntry> subProgress) {
                var pieces = entry.FullName.Split('/', '\\').ToList();
                if (pieces.Count <= 3 || pieces[0] != @"MODS" || pieces[1] != _modsDirectory || pieces[2] != @"extension"
                        || pieces.Count == 4 && pieces[3] == string.Empty) {
                    Logging.Debug("Skipping: " + entry.FullName);
                    return;
                }

                var destination = Path.GetFullPath(Path.Combine(_patchLocation, pieces.Skip(3).JoinToString('/')));
                if (!destination.StartsWith(_patchLocation, StringComparison.OrdinalIgnoreCase)) {
                    Logging.Debug($"Wrong destination: {destination}");
                    return;
                }

                ExtractFile(entry, destination, () => subProgress.Report($"Extracting {_stageName} file: {entry.Name}", i, count));
            }

            private void ProcessForced(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation, ZipArchive archive) {
                foreach (var relative in _forcedFiles) {
                    Logging.Debug($"Processing forced file: {relative}");

                    var forcedFileEntry = archive.GetEntry($@"MODS/{_modsDirectory}/{FileUtils.NormalizePath(relative).Replace('\\', '/')}");
                    if (forcedFileEntry == null) {
                        throw new InformativeException("Can’t update patch", $"Path file “{Path.GetFileName(relative)}” is missing.");
                    }

                    var forcedFileDestination = Path.Combine(AcRootDirectory.Instance.RequireValue, relative);
                    Logging.Debug($"Forced file destination: {forcedFileDestination}");

                    progress.Report($"Removing existing: {Path.GetFileName(relative)}", 0.85f);
                    RemoveForcedFile(forcedFileDestination, cancellation);
                    if (cancellation.IsCancellationRequested) return;

                    progress.Report($"Extracting main file: {Path.GetFileName(relative)}", 0.83f);
                    ExtractFileNoChecks(forcedFileEntry, forcedFileDestination);
                    if (cancellation.IsCancellationRequested) return;
                }
            }

            private void ExtractFile(ZipArchiveEntry entry, string destination, Action progressReport) {
                if (string.IsNullOrWhiteSpace(destination)) return;

                var relative = FileUtils.GetRelativePath(destination, _patchLocation);

                if (Path.GetFileName(entry.FullName) == string.Empty) {
                    Logging.Debug($"Creating directory: {destination}");
                    progressReport();
                    if (TryToDoAFile(() => Directory.CreateDirectory(destination))) {
                        _installationLog?.WriteLine(@"directory: " + relative);
                        var removed = _directoriesToRemove.RemoveAll(x => FileUtils.ArePathsEqual(relative, x));
                        Logging.Debug($"Removed from list of designated to deletion: {removed}");
                    }
                    return;
                }

                if (Directory.Exists(destination)) {
                    Logging.Error($"Can’t extract file “{entry.FullName}”: there is a directory “{destination}” in its place.");
                    return;
                }

                var existing = _filesToRemove.FirstOrDefault(x => FileUtils.ArePathsEqual(x.File.FullName, destination));
                using (var unpackedStream = new MemoryStream()) {
                    using (var entryStream = entry.Open()) {
                        entryStream.CopyTo(unpackedStream);
                    }

                    var data = unpackedStream.ToArray();
                    var hash = new xxHash();
                    hash.Init();
                    hash.Update(data, data.Length);

                    var checksum = hash.Digest().ToString();
                    if (existing != null) {
                        existing.ToRecycle = false;
                        if (existing.File.Exists && existing.Checksum == checksum) {
                            Logging.Debug($"Perfectly matches existing: {existing.File.FullName}");
                            _installationLog?.WriteLine(@"file: " + relative + @":" + checksum + @":" + entry.Length +
                                    @":" + existing.LastWrite.ToUnixTimestamp());
                            return;
                        }
                        Logging.Debug($"Stop existing from recycling: {existing.File.FullName}");
                    } else {
                        Logging.Debug($"New file: {entry.FullName}");
                    }

                    progressReport();
                    if (TryToDoAFile(() => {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? "");
                        File.WriteAllBytes(destination, unpackedStream.ToArray());
                        File.SetLastWriteTime(destination, entry.LastWriteTime.DateTime);
                    })) {
                        _installationLog?.WriteLine(@"file: " + relative + @":" + checksum + @":" + entry.Length +
                                @":" + entry.LastWriteTime.DateTime.ToUnixTimestamp());
                    }
                }
            }

            private static void ExtractFileNoChecks(ZipArchiveEntry entry, string destination) {
                if (string.IsNullOrWhiteSpace(destination)) return;

                Logging.Debug($"Extracting file without any checks: {entry.FullName}, to {destination}");
                if (Path.GetFileName(entry.FullName) == string.Empty) {
                    TryToDoAFileOrThrow(() => Directory.CreateDirectory(destination));
                } else {
                    if (Directory.Exists(destination)) {
                        throw new InformativeException($"Can’t extract file “{entry.FullName}”",
                                $"There is a directory “{destination}” in its place.");
                    }
                    TryToDoAFileOrThrow(() => {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? "");
                        entry.ExtractToFile(destination, true);
                    });
                }
            }

            private static bool TryToDoAFile(Action action) {
                for (var j = 3; j >= 0; j--) {
                    try {
                        action();
                        return true;
                    } catch (IOException e) {
                        Logging.Warning(e);
                        Thread.Sleep(30);
                    }
                }
                return false;
            }

            private static void TryToDoAFileOrThrow(Action action) {
                for (var j = 3; j >= 0; j--) {
                    try {
                        action();
                        return;
                    } catch (IOException e) {
                        if (j == 0) throw;
                        Logging.Warning(e);
                        Thread.Sleep(30);
                    }
                }
            }

            private static void RemoveForcedFile(string filename, CancellationToken cancellation) {
                Logging.Debug($"Removing forced file: {filename}");

                for (var i = 10; i >= 0 && File.Exists(filename); i--) {
                    try {
                        File.Delete(filename);
                        break;
                    } catch (IOException e) {
                        if (i == 0) throw;
                        Logging.Warning(e.Message);
                        Thread.Sleep(30);
                    } catch (UnauthorizedAccessException e) {
                        if (i == 0) throw;
                        Logging.Warning(e.Message);
                        Thread.Sleep(30);
                    }
                    if (cancellation.IsCancellationRequested) return;
                }

                if (File.Exists(filename)) {
                    throw new InformativeException("Can’t update patch", $"All attempts to remove existing “{Path.GetFileName(filename)}” failed.");
                }
            }

            public string ToRemoveLine() {
                return $"{_filesToRemove.Count} files, {_directoriesToRemove.Count} directories";
            }

            public void Clear() {
                _filesToRemove.Clear();
                _directoriesToRemove.Clear();
            }
        }

        private class InstallVars {
            public InstallStage PatchStage, ChunkStage;

            public string InstalledVersion;
            public int InstalledBuild = -1;
            public string InstalledChunk;

            public void LoadValues(ref IProgress<AsyncProgressEntry> progress) {
                PatchStage = new InstallStage("patch", @"Shaders Lights Patch", new[] { PatchHelper.MainFileName });
                ChunkStage = new InstallStage("chunk", @"Shaders Lights Patch - Data", new string[0]);
                var fillingStage = PatchStage;

                var location = PatchHelper.GetRootDirectory();
                var installedLogFilename = PatchHelper.GetInstalledLog();

                if (File.Exists(installedLogFilename)) {
                    Logging.Debug($"Found some info on current installation in {installedLogFilename}");
                    foreach (var line in File.ReadAllLines(installedLogFilename).Select(x => x.Trim()).Where(x => !x.StartsWith(@"#"))) {
                        var keyValue = line.Split(new[] { ':' }, 2, StringSplitOptions.None);
                        if (keyValue.Length != 2) continue;
                        switch (keyValue[0].Trim()) {
                            case "version":
                                InstalledVersion = keyValue[1].Trim();
                                break;
                            case "build":
                                InstalledBuild = keyValue[1].Trim().As(-1);
                                break;
                            case "chunk":
                                InstalledChunk = keyValue[1].Trim();
                                fillingStage = ChunkStage;
                                break;
                            case "directory":
                                fillingStage.AddDirectoryToRemove(Path.Combine(location, keyValue[1].Trim()));
                                break;
                            case "file":
                                var pieces = keyValue[1].Split(new[] { ':' }, 4, StringSplitOptions.None);
                                if (pieces.Length == 4) {
                                    fillingStage.AddFileToRemove(new InstalledFile {
                                        File = new FileInfo(Path.Combine(location, pieces[0].Trim())),
                                        Checksum = pieces[1].Trim(),
                                        Length = pieces[2].As(0L),
                                        LastWrite = pieces[3].As(0L).ToDateTime(),
                                    });
                                }
                                break;
                        }
                    }

                    Logging.Debug($"Currently installed: v{InstalledVersion} ({InstalledBuild}), {PatchStage.ToRemoveLine()}");
                    if (InstalledChunk != null) {
                        Logging.Debug($"Currently installed chunk: v{InstalledChunk}, {ChunkStage.ToRemoveLine()}");
                    }
                }

                if (InstalledBuild != PatchHelper.GetInstalledBuild().As(-1)) {
                    Logging.Warning("Versions in data_manifest.ini and installed.log are out of sync. Reinstalling from scratch.");
                    Logging.Warning("From log: " + InstalledBuild);
                    Logging.Warning("From manifest: " + PatchHelper.GetInstalledBuild());

                    var split = progress?.Split(0.2);
                    split?.Item1.Report("Recycling current configs", 0.5);

                    var configsDirectory = Path.Combine(location, "configs");
                    if (Directory.Exists(configsDirectory)) {
                        FileUtils.Recycle(Directory.GetFiles(configsDirectory, "*.ini"));
                    }

                    progress = split?.Item2;
                    InstalledVersion = null;
                    InstalledBuild = -1;
                    InstalledChunk = null;
                    PatchStage.Clear();
                    ChunkStage.Clear();
                }
            }
        }

        public static async Task RemovePatch(bool includeChunk, IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            if (_installing) throw new InformativeException("Can’t remove patch", "Another update is in process.");
            _installing = true;

            try {
                _installing = true;

                Logging.Debug($"Removing current patch installation");
                if (!Directory.Exists(PatchHelper.GetRootDirectory())) return;

                var vars = new InstallVars();
                vars.LoadValues(ref progress);

                if (vars.InstalledVersion == null) {
                    Logging.Warning("Current installation is damaged, removing files in safe mode");

                    await Task.Run(() => {
                        var toRemove = new List<string>();

                        var configsDirectory = Path.Combine(PatchHelper.GetRootDirectory(), "config");
                        if (Directory.Exists(configsDirectory)) {
                            toRemove.AddRange(Directory.GetFiles(configsDirectory, "*.ini"));
                            toRemove.AddRange(Directory.GetFiles(configsDirectory, "*.txt"));
                        }

                        var luaDirectory = Path.Combine(PatchHelper.GetRootDirectory(), "lua");
                        if (Directory.Exists(luaDirectory)) {
                            toRemove.AddRange(Directory.GetFiles(luaDirectory, "*.lua"));
                        }

                        var shadersDirectory = Path.Combine(PatchHelper.GetRootDirectory(), "shaders");
                        if (Directory.Exists(shadersDirectory)) {
                            toRemove.Add(shadersDirectory);
                        }

                        var shadersPack = Path.Combine(PatchHelper.GetRootDirectory(), "shaders.zip");
                        if (File.Exists(shadersPack)) {
                            toRemove.Add(shadersPack);
                        }

                        FileUtils.Recycle(toRemove.ToArray());
                    }).ConfigureAwait(false);
                } else {
                    await Task.Run(() => {
                        vars.PatchStage.Run(new byte[0], null, progress.Subrange(0.02, includeChunk ? 0.48 : 0.96), cancellation);
                        if (includeChunk) {
                            vars.ChunkStage.Run(new byte[0], null, progress.Subrange(0.51, 0.48), cancellation);
                        }
                    }).ConfigureAwait(false);
                }

                FileUtils.TryToDelete(PatchHelper.GetInstalledLog());
                FileUtils.TryToDelete(Path.Combine(PatchHelper.GetRootDirectory(), "config", "data_manifest.ini"));
                PatchHelper.Reload();
            } finally {
                _installing = false;
            }
        }

        private AsyncCommand<CancellationToken?> _installCommand;

        public AsyncCommand<CancellationToken?> InstallCommand => _installCommand ??
                (_installCommand = new AsyncCommand<CancellationToken?>(c => PatchUpdater.Instance.InstallAsync(this, c ?? default)));

        public async Task InstallAsync(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            if (_installing) throw new InformativeException("Can’t update patch", "Another update is in process.");
            _installing = true;

            try {
                if (!AvailableToDownload) {
                    throw new InformativeException("Can’t update patch", "This version is not available.");
                }

                _installing = true;
                Logging.Debug($"Beginning patch installation, trying to install v{Version} ({Build})");

                var vars = new InstallVars();
                vars.LoadValues(ref progress);

                byte[] dataPatch;
                if (Version != vars.InstalledVersion && Build != vars.InstalledBuild) {
                    dataPatch = await CmApiProvider.GetPatchVersionAsync(Version, progress.Subrange(0.0, 0.3), cancellation);
                    if (dataPatch == null) throw new InformativeException(ToolsStrings.AppUpdater_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks);
                    if (cancellation.IsCancellationRequested) return;
                    Logging.Debug("Zipped patch ready: " + dataPatch.Length);
                } else {
                    dataPatch = null;
                    Logging.Debug("Skipping patch stage");
                }

                byte[] dataChunk;
                if (ChunkVersion == null) {
                    dataChunk = new byte[0];
                    Logging.Debug("No chunk needed");
                } else if (ChunkVersion != vars.InstalledChunk) {
                    dataChunk = await CmApiProvider.GetChunkVersionAsync(ChunkVersion, progress.Subrange(0.3, 0.5), cancellation);
                    if (dataChunk == null) throw new InformativeException(ToolsStrings.AppUpdater_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks);
                    if (cancellation.IsCancellationRequested) return;
                    Logging.Debug("Zipped chunk ready: " + dataChunk.Length);
                } else {
                    dataChunk = null;
                    Logging.Debug("Skipping chunk stage");
                }

                await Task.Run(() => {
                    Logging.Debug("Main folder created");
                    Directory.CreateDirectory(PatchHelper.GetRootDirectory());

                    using (var installedLogStream = new MemoryStream()) {
                        using (var installedLog = new StreamWriter(installedLogStream)) {
                            installedLog.WriteLine(@"# Generated automatically during last patch installation via Content Manager.");
                            installedLog.WriteLine(@"# Do not edit, unless have to for some reason.");

                            installedLog.WriteLine(@"version: " + Version);
                            installedLog.WriteLine(@"build: " + Build);
                            if (dataPatch != null) {
                                Logging.Debug("Running patch stage");
                                vars.PatchStage.Run(dataPatch, installedLog, progress.Subrange(0.5, 0.245), cancellation);
                            }

                            installedLog.WriteLine(@"chunk: " + ChunkVersion);
                            if (dataChunk != null) {
                                Logging.Debug("Running chunk stage");
                                vars.ChunkStage.Run(dataChunk, installedLog, progress.Subrange(0.75, 0.245), cancellation);
                            }
                        }
                        File.WriteAllBytes(PatchHelper.GetInstalledLog(), installedLogStream.ToArray());
                    }

                    PatchHelper.Reload();
                });
            } finally {
                _installing = false;
            }
        }

        [ItemNotNull]
        public static async Task<IReadOnlyCollection<PatchVersionInfo>> GetPatchManifestAsync(IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default) {
            try {
                var t = await CmApiProvider.GetPatchDataAsync(CmApiProvider.PatchDataType.Manifest, string.Empty,
                        TimeSpan.FromMinutes(30.0), progress, cancellation);
                if (t == null) {
                    throw new InformativeException(ToolsStrings.AppUpdater_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks);
                }
                var result = JsonConvert.DeserializeObject<PatchVersionInfo[]>(await FileUtils.ReadAllTextAsync(t.Item1));
                result.ForEach(x => x.Changelog = ChangelogPrepare(x.Changelog));
                return result;
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t load list of patch versions", ToolsStrings.ContentSyncronizer_CannotLoadContent_Commentary, e);
                return new List<PatchVersionInfo>();
            }
        }

        private static string TagRegexCallback(Match m) {
            string tag;
            switch (m.Groups[1].Value) {
                case "*":
                    tag = "i";
                    break;

                case "~":
                    tag = "s";
                    break;

                case "`":
                    tag = "mono";
                    break;

                default:
                    return m.Value;
            }

            return "[" + tag + "]" + m.Groups[2].Value + "[/" + tag + "]";
        }

        private static string RepeatString(string s, int number) {
            if (s == null) return null;
            switch (number) {
                case 0:
                    return string.Empty;
                case 1:
                    return s;
                case 2:
                    return s + s;
                default:
                    var b = new StringBuilder();
                    for (var i = 0; i < number; i++) {
                        b.Append(s);
                    }
                    return b.ToString();
            }
        }

        private static readonly Regex NextCursiveFixRegex = new Regex(@"^(\s*)(-.+\n.+\n)\s*(?=\*\w)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex CursiveFixRegex = new Regex(@"^(\s*)(-.+\n)\s*(?=\*\w)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex ListRegex = new Regex(@"^([ \t]*)-", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex ImageRegex = new Regex(@"!\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex BoldRegex = new Regex(@"(?!<\\)\*\*([\s\S]+?)(?!<\\)\*\*", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex TagRegex = new Regex(@"(?!<\\)([*~`])([\s\S]+?)(?!<\\)\1", RegexOptions.Compiled | RegexOptions.Multiline);

        public static string ChangelogPrepare(string s) {
            s = Regex.Replace(s, @"(?<=\n)###+\s*(.+)", "[b]$1[/b]").Trim();
            s = NextCursiveFixRegex.Replace(s, m => $"{m.Groups[1].Value}{m.Groups[2].Value}   {RepeatString("  ", m.Groups[1].Length)}");
            s = CursiveFixRegex.Replace(s, m => $"{m.Groups[1].Value}{m.Groups[2].Value}   {RepeatString("  ", m.Groups[1].Length)}");
            s = ListRegex.Replace(s, m => $" {RepeatString("  ", m.Groups[1].Length)}{(m.Groups[1].Length < 2 ? "•" : "◦")}");
            s = ImageRegex.Replace(s, " [img=\"$2|240\"]$1[/img]");
            s = LinkRegex.Replace(s, "[url=\"$2\"]$1[/url]");
            s = BoldRegex.Replace(s, "[b]$1[/b]");
            s = TagRegex.Replace(s, TagRegexCallback);
            s = TagRegex.Replace(s, TagRegexCallback);
            s = TagRegex.Replace(s, TagRegexCallback);
            return s;
        }
    }
}