using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public enum ServerPresetPackMode {
        [Description("Windows")]
        Windows,

        [Description("Linux (32-bit)")]
        Linux32,

        [Description("Linux (64-bit)")]
        Linux64,
    }

    public partial class ServerPresetObject {
        private static readonly string[] TrackDataToKeep = {
            @"surfaces.ini", @"drs_zones.ini"
        };

        public class PackedEntry : IDisposable {
            public readonly string Key;

            private string _filename;
            private bool _temporaryFilename;
            private readonly byte[] _content;

            private PackedEntry(string key, string filename, byte[] content) {
                Key = key;
                _filename = filename;
                _content = content;
            }

            [CanBeNull]
            public string GetFilename(string temporaryDirectory) {
                if (_filename == null) {
                    if (_content == null) return null;

                    _filename = FileUtils.GetTempFileName(temporaryDirectory, Path.GetExtension(Key));
                    _temporaryFilename = true;
                    File.WriteAllBytes(_filename, _content);
                }

                return _filename;
            }

            [CanBeNull]
            public byte[] GetContent() {
                return _content ?? (_filename != null && File.Exists(_filename) ? File.ReadAllBytes(_filename) : null);
            }

            public static PackedEntry FromFile(string key, string filename) {
                return new PackedEntry(key, filename, null);
            }

            public static PackedEntry FromContent(string key, string content) {
                return new PackedEntry(key, null, Encoding.UTF8.GetBytes(content));
            }

            public static PackedEntry FromContent(string key, byte[] content) {
                return new PackedEntry(key, null, content);
            }

            public void Dispose() {
                if (_temporaryFilename) {
                    File.Delete(_filename);
                    _filename = null;
                }
            }
        }

        [ItemCanBeNull]
        public async Task<List<PackedEntry>> PackServerData(bool saveExecutable, ServerPresetPackMode mode, bool evbMode, CancellationToken cancellation) {
            var result = new List<PackedEntry>();

            // Wrapper
            if (WrapperUsed) {
                if (saveExecutable) {
                    string wrapper;
                    switch (mode) {
                        case ServerPresetPackMode.Linux32:
                            wrapper = await LoadLinux32Wrapper(cancellation);
                            break;
                        case ServerPresetPackMode.Linux64:
                            wrapper = await LoadLinux64Wrapper(cancellation);
                            break;
                        case ServerPresetPackMode.Windows:
                            wrapper = await LoadWinWrapper(cancellation);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                    }

                    if (cancellation.IsCancellationRequested) return null;

                    if (wrapper == null) {
                        throw new InformativeException("Can’t pack server", "Can’t load server wrapper.");
                    }

                    // Actual wrapper, compiled to a single exe-file
                    result.Add(PackedEntry.FromFile(
                            mode == ServerPresetPackMode.Windows ? "acServerWrapper.exe" : "acServerWrapper",
                            wrapper));

                    // For EVB
                    if (evbMode) {
                        result.Add(PackedEntry.FromContent("arguments.json", new JArray {
                            "--copy-executable-to=acServer_tmp.exe",
                        }.ToString(Formatting.Indented)));
                    }
                }

                // Params
                var wrapperParams = _wrapperParamsJson?.DeepClone() as JObject;
                SetWrapperParams(ref wrapperParams);
                result.Add(PackedEntry.FromContent("cfg/cm_wrapper_params.json", wrapperParams.ToString(Formatting.Indented)));

                // Content
                if (WrapperContentJObject != null) {
                    void ProcessPiece(JObject piece) {
                        var file = (string)piece?["file"];
                        if (piece == null || file == null) return;

                        var filename = Path.IsPathRooted(file) ? file : Path.Combine(WrapperContentDirectory, file);
                        if (!FileUtils.ArePathsEqual(WrapperContentDirectory, Path.GetDirectoryName(filename))) {
                            piece["file"] = Path.GetFileName(filename);
                        }

                        result.Add(PackedEntry.FromFile($"cfg/cm_content/{Path.GetFileName(filename)}", filename));
                    }

                    void ProcessPieces(JToken token, string childrenKey = null) {
                        var o = token as JObject;
                        if (o == null) return;
                        foreach (var t in o) {
                            var b = (JObject)t.Value;
                            if (b == null) continue;
                            ProcessPiece(b);
                            if (childrenKey != null) {
                                ProcessPieces(b[childrenKey]);
                            }
                        }
                    }

                    var content = WrapperContentJObject.DeepClone();
                    ProcessPieces(content["cars"], "skins");
                    ProcessPiece(content["track"] as JObject);
                    ProcessPieces(content["weather"]);
                    result.Add(PackedEntry.FromContent("cfg/cm_content/content.json", content.ToString(Formatting.Indented)));
                } else {
                    result.Add(PackedEntry.FromContent("cfg/cm_content/content.json", "{}"));
                }
            }

            // Executable
            if (saveExecutable) {
                var serverDirectory = ServerPresetsManager.ServerDirectory;
                result.Add(PackedEntry.FromFile(
                        mode == ServerPresetPackMode.Windows ? "acServer.exe" : "acServer",
                        Path.Combine(serverDirectory, mode == ServerPresetPackMode.Windows ? "acServer.exe" : "acServer")));
            }

            // Welcome message
            if (!string.IsNullOrEmpty(WelcomeMessage)) {
                result.Add(PackedEntry.FromContent("cfg/welcome.txt", WelcomeMessage));
            }

            // Main config file
            var serverCfg = IniObject?.Clone() ?? new IniFile(IniFileMode.ValuesWithSemicolons);
            SaveData(serverCfg);

            if (!string.IsNullOrEmpty(WelcomeMessage)) {
                serverCfg["SERVER"].Set("WELCOME_MESSAGE", "cfg/welcome.txt");
            }

            if (WrapperUsed) {
                serverCfg["SERVER"].Set("NAME", $"{Name} {ServerEntry.ExtendedSeparator}{WrapperPort}");
            }

            result.Add(PackedEntry.FromContent("cfg/server_cfg.ini", serverCfg.Stringify()));

            // Entry list
            var entryList = EntryListIniObject?.Clone() ?? new IniFile();
            entryList.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section));
            result.Add(PackedEntry.FromContent("cfg/entry_list.ini", entryList.Stringify()));

            // Cars
            var root = AcRootDirectory.Instance.RequireValue;
            for (var i = 0; i < CarIds.Length; i++) {
                var carId = CarIds[i];
                var packedData = Path.Combine(AcPaths.GetCarDirectory(root, carId), "data.acd");
                if (File.Exists(packedData)) {
                    result.Add(PackedEntry.FromFile(Path.Combine(@"content", @"cars", carId, @"data.acd"), packedData));
                }
            }

            // Track
            var localPath = TrackLayoutId != null ? Path.Combine(TrackId, TrackLayoutId) : TrackId;
            foreach (var file in TrackDataToKeep) {
                var actualData = Path.Combine(AcPaths.GetTracksDirectory(root), localPath, @"data", file);
                if (File.Exists(actualData)) {
                    result.Add(PackedEntry.FromFile(Path.Combine(@"content", @"tracks", localPath, @"data", file), actualData));
                }
            }

            // System
            var systemSurfaces = Path.Combine(ServerPresetsManager.ServerDirectory, "system", "data", "surfaces.ini");
            if (File.Exists(systemSurfaces)) {
                result.Add(PackedEntry.FromFile("system/data/surfaces.ini", systemSurfaces));
            }

            return result;
        }

        private static void PrepareCar([NotNull] string carId) {
            var root = AcRootDirectory.Instance.RequireValue;
            var actualData = new FileInfo(Path.Combine(AcPaths.GetCarDirectory(root, carId), "data.acd"));
            var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"cars", carId, @"data.acd"));

            if (actualData.Exists && (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime)) {
                Directory.CreateDirectory(serverData.DirectoryName ?? "");
                FileUtils.HardLinkOrCopy(actualData.FullName, serverData.FullName, true);
            }
        }

        private static void PrepareTrack([NotNull] string trackId, [CanBeNull] string configurationId) {
            var root = AcRootDirectory.Instance.RequireValue;
            var localPath = configurationId != null ? Path.Combine(trackId, configurationId) : trackId;

            foreach (var file in TrackDataToKeep) {
                var actualData = new FileInfo(Path.Combine(AcPaths.GetTracksDirectory(root), localPath, @"data", file));
                var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"tracks", localPath, @"data", file));

                if (actualData.Exists && (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime)) {
                    Directory.CreateDirectory(serverData.DirectoryName ?? "");
                    FileUtils.HardLinkOrCopy(actualData.FullName, serverData.FullName, true);
                }
            }
        }

        /// <summary>
        /// Update data in server’s folder according to actual data.
        /// </summary>
        public async Task PrepareServer(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            for (var i = 0; i < CarIds.Length; i++) {
                var carId = CarIds[i];
                progress?.Report(new AsyncProgressEntry(carId, i, CarIds.Length + 1));
                PrepareCar(carId);

                await Task.Delay(10, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }

            progress?.Report(new AsyncProgressEntry(TrackId, CarIds.Length, CarIds.Length + 1));
            PrepareTrack(TrackId, TrackLayoutId);
        }

        public static string GetServerExecutableFilename() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, @"server", @"acServer.exe");
        }

        public void StopServer() {
            if (IsRunning) {
                _running?.Kill();
                SetRunning(null);
            }
        }

        private string _inviteCommand;

        public string InviteCommand {
            get { return _inviteCommand; }
            set {
                if (Equals(value, _inviteCommand)) return;
                _inviteCommand = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Start server (all stdout stuff will end up in RunningLog).
        /// </summary>
        /// <exception cref="InformativeException">For some predictable errors.</exception>
        /// <exception cref="Exception">Process starting might cause loads of problems.</exception>
        public async Task RunServer(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            StopServer();

            if (!Enabled) {
                throw new InformativeException("Can’t run server", "Preset is disabled.");
            }

            if (HasErrors) {
                throw new InformativeException("Can’t run server", "Preset has errors.");
            }

            if (TrackId == null) {
                throw new InformativeException("Can’t run server", "Track is not specified.");
            }

            var serverExecutable = GetServerExecutableFilename();
            if (!File.Exists(serverExecutable)) {
                throw new InformativeException("Can’t run server", "Server’s executable not found.");
            }

            if (SettingsHolder.Online.ServerPresetsAutoSave) {
                SaveCommand.Execute();
            }

            if (SettingsHolder.Online.ServerPresetsUpdateDataAutomatically) {
                await PrepareServer(progress, cancellation);
            }

            var welcomeMessageLocal = IniObject?["SERVER"].GetNonEmpty("WELCOME_MESSAGE");
            var welcomeMessageFilename = WelcomeMessagePath;
            if (welcomeMessageLocal != null && welcomeMessageFilename != null && File.Exists(welcomeMessageFilename)) {
                using (FromServersDirectory()) {
                    var local = new FileInfo(welcomeMessageLocal);
                    if (!local.Exists || new FileInfo(welcomeMessageFilename).LastWriteTime > local.LastWriteTime) {
                        try {
                            File.Copy(welcomeMessageFilename, welcomeMessageLocal, true);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                }
            }

            var log = new BetterObservableCollection<string>();
            RunningLog = log;

            // await

            if (WrapperUsed) {
                await RunWrapper(serverExecutable, log, progress, cancellation);
            } else {
                await RunAcServer(serverExecutable, log, progress, cancellation);
            }
        }

        [ItemCanBeNull]
        private async Task<string> LoadWinWrapper(CancellationToken cancellation) {
            var wrapperFilename = FilesStorage.Instance.GetFilename("Server Wrapper", "AcServerWrapper.exe");

            var data = await CmApiProvider.GetStaticDataAsync("ac_server_wrapper");
            if (cancellation.IsCancellationRequested || data == null) return null;

            if (data.Item2) {
                // Freshly loaded
                var wrapperBytes = await FileUtils.ReadAllBytesAsync(data.Item1);
                if (cancellation.IsCancellationRequested) return null;

                await Task.Run(() => {
                    using (var stream = new MemoryStream(wrapperBytes, false))
                    using (var archive = new ZipArchive(stream)){
                        try {
                            File.WriteAllBytes(wrapperFilename, archive.GetEntry("acServerWrapper.exe").Open().ReadAsBytesAndDispose());
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                });
            }

            return File.Exists(wrapperFilename) ? wrapperFilename : null;
        }

        [ItemCanBeNull]
        private async Task<string> LoadLinux32Wrapper(CancellationToken cancellation) {
            return (await CmApiProvider.GetStaticDataAsync("ac_server_wrapper-linux-x86"))?.Item1;
        }

        [ItemCanBeNull]
        private async Task<string> LoadLinux64Wrapper(CancellationToken cancellation) {
            return (await CmApiProvider.GetStaticDataAsync("ac_server_wrapper-linux-x64"))?.Item1;
        }

        private async Task RunWrapper(string serverExecutable, ICollection<string> log, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            progress.Report(AsyncProgressEntry.FromStringIndetermitate("Loading wrapper…"));
            var wrapperFilename = await LoadWinWrapper(cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (wrapperFilename == null) {
                throw new InformativeException("Can’t run server", "Can’t load server wrapper.");
            }

            try {
                var now = DateTime.Now;
                var logName = FileUtils.EnsureFileNameIsValid(
                        $"Server_{DisplayName}_{now.Year % 100:D2}{now.Month:D2}{now.Day:D2}_{now.Hour:D2}{now.Minute:D2}{now.Second:D2}.log");
                using (var writer = new StreamWriter(FilesStorage.Instance.GetFilename("Logs", logName), false))
                using (var process = ProcessExtension.Start(wrapperFilename, new[] {
                    "-e", serverExecutable, $"presets/{Id}"
                }, new ProcessStartInfo {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(serverExecutable) ?? "",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                })) {
                    process.Start();
                    SetRunning(process);
                    ChildProcessTracker.AddProcess(process);

                    progress?.Report(AsyncProgressEntry.Finished);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.OutputDataReceived += (sender, args) => {
                        if (!string.IsNullOrWhiteSpace(args.Data)) {
                            writer.WriteLine(args.Data);
                            ActionExtension.InvokeInMainThread(() => log.Add(Regex.Replace(args.Data, @"\b(WARNING: .+)", @"[color=#ff8800]$1[/color]")));
                        }
                    };

                    process.ErrorDataReceived += (sender, args) => {
                        if (!string.IsNullOrWhiteSpace(args.Data)) {
                            writer.WriteLine(args.Data);
                            ActionExtension.InvokeInMainThread(() => log.Add($@"[color=#ff0000]{args.Data}[/color]"));
                        }
                    };

                    await process.WaitForExitAsync(cancellation);
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }

                    log.Add($@"[CM] Stopped: {process.ExitCode}");
                }
            } finally {
                SetRunning(null);
            }
        }

        private async Task RunAcServer(string serverExecutable, ICollection<string> log, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            try {
                using (var process = new Process {
                    StartInfo = {
                        FileName = serverExecutable,
                        Arguments = $"-c presets/{Id}/server_cfg.ini -e presets/{Id}/entry_list.ini",
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(serverExecutable) ?? "",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding =  Encoding.UTF8,
                    }
                }) {
                    process.Start();
                    SetRunning(process);
                    ChildProcessTracker.AddProcess(process);

                    progress?.Report(AsyncProgressEntry.Finished);

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.OutputDataReceived += (sender, args) => ActionExtension.InvokeInMainThread(() => log.Add(args.Data));
                    process.ErrorDataReceived += (sender, args) => ActionExtension.InvokeInMainThread(() => log.Add($@"[color=#ff0000]{args.Data}[/color]"));

                    await process.WaitForExitAsync(cancellation);
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }

                    log.Add($@"[CM] Stopped: {process.ExitCode}");
                }
            } finally {
                SetRunning(null);
            }
        }

        private Process _running;

        private void SetRunning(Process running) {
            _running = running;
            OnPropertyChanged(nameof(IsRunning));
            _stopServerCommand?.RaiseCanExecuteChanged();
            _runServerCommand?.RaiseCanExecuteChanged();
            _restartServerCommand?.RaiseCanExecuteChanged();
        }

        public override void Reload() {
            if (IsRunning) {
                try {
                    _running.Kill();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            base.Reload();
        }

        public bool IsRunning => _running != null;

        private BetterObservableCollection<string> _runningLog;

        public BetterObservableCollection<string> RunningLog {
            get { return _runningLog; }
            set {
                if (Equals(value, _runningLog)) return;
                _runningLog = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _stopServerCommand;

        public DelegateCommand StopServerCommand => _stopServerCommand ?? (_stopServerCommand = new DelegateCommand(StopServer, () => IsRunning));

        private AsyncCommand _runServerCommand;

        public AsyncCommand RunServerCommand => _runServerCommand ??
                (_runServerCommand = new AsyncCommand(() => RunServer(), () => Enabled && !HasErrors && !IsRunning));

        private AsyncCommand _restartServerCommand;

        public AsyncCommand RestartServerCommand => _restartServerCommand ??
                (_restartServerCommand = new AsyncCommand(() => RunServer(), () => Enabled && !HasErrors && IsRunning));
    }
}
