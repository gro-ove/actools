using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.ServerPlugins;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public enum ServerPresetPackMode {
        [Description("Windows")]
        Windows = 0,

        [Description("Linux (32-bit)")]
        Linux32 = 1,

        [Description("Linux (64-bit)")]
        Linux64 = 2,
    }

    public partial class ServerPresetObject {
        private static readonly string[] TrackDataToKeep = {
            @"surfaces.ini", @"drs_zones.ini"
        };

        public class PackedEntry : IDisposable {
            public readonly string Key;
            public bool IsExecutable { get; }

            private string _filename;
            private bool _temporaryFilename;
            private readonly byte[] _content;

            private PackedEntry(string key, string filename, byte[] content) {
                Key = key;
                _filename = filename;
                _content = content;

                IsExecutable = Path.GetExtension(key) != @".ini" && Path.GetExtension(key) != @".acd";
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

            public static PackedEntry FromFile([Localizable(false)] string key, string filename) {
                return new PackedEntry(key, filename, null);
            }

            public static PackedEntry FromContent([Localizable(false)] string key, string content) {
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

        private bool TrackPreprocess(string filename, out string pathPrefix, out string newContent) {
            if (CspRequiredActual && string.Equals(Path.GetFileName(filename), @"surfaces.ini", StringComparison.OrdinalIgnoreCase)) {
                pathPrefix = "csp";
                newContent = File.ReadAllText(filename).Replace("[SURFACE_0]", "[CSPFACE_0]");
                return true;
            }

            pathPrefix = null;
            newContent = null;
            return false;
        }

        private async Task<IniFile> GetBaseMainConfigAsync() {
            var serverCfg = IniObject?.Clone() ?? new IniFile(IniFileMode.ValuesWithSemicolons);
            SaveData(serverCfg);

            foreach (var sectionKey in serverCfg.Keys.Where(x => x.StartsWith("__CM_")).ToList()) {
                serverCfg.Remove(sectionKey);
            }
            foreach (var section in serverCfg.Values) {
                foreach (var key in section.Keys.Where(x => x.StartsWith("__CM_")).ToList()) {
                    section.Remove(key);
                }
            }

            if (ProvideDetails) {
                if (DetailsMode == ServerPresetDetailsMode.ViaWrapper) {
                    serverCfg["SERVER"].Set("NAME", $"{Name} {ServerEntry.ExtendedSeparator}{WrapperPort}");
                } else {
                    await EnsureDetailsNameIsActualAsync(serverCfg);
                }
            }
            return serverCfg;
        }

        [ItemCanBeNull]
        public async Task<List<PackedEntry>> PackServerData(bool saveExecutable, ServerPresetPackMode mode, bool evbMode, CancellationToken cancellation) {
            var result = new List<PackedEntry>();

            // Wrapper
            if (ProvideDetails && DetailsMode == ServerPresetDetailsMode.ViaWrapper) {
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
                            mode == ServerPresetPackMode.Windows ? @"acServerWrapper.exe" : @"acServerWrapper",
                            wrapper));

                    // For EVB
                    if (evbMode) {
                        result.Add(PackedEntry.FromContent("arguments.json", new JArray {
                            @"--copy-executable-to=acServer_tmp.exe",
                        }.ToString(Formatting.Indented)));
                    }
                }

                // Params
                var wrapperParams = _wrapperParamsJson?.DeepClone() as JObject;
                SetWrapperParams(ref wrapperParams);
                result.Add(PackedEntry.FromContent(@"cfg/cm_wrapper_params.json", wrapperParams.ToString(Formatting.Indented)));

                // Content
                if (DetailsContentJObject != null) {
                    void ProcessPiece(JObject piece) {
                        var file = (string)piece?[@"file"];
                        if (piece == null || file == null) return;

                        var filename = Path.IsPathRooted(file) ? file : Path.Combine(WrapperContentDirectory, file);
                        if (!FileUtils.ArePathsEqual(WrapperContentDirectory, Path.GetDirectoryName(filename) ?? "")) {
                            piece[@"file"] = Path.GetFileName(filename);
                        }

                        result.Add(PackedEntry.FromFile($"cfg/cm_content/{Path.GetFileName(filename)}", filename));
                    }

                    void ProcessPieces(JToken token, string childrenKey = null) {
                        if (!(token is JObject o)) return;
                        foreach (var t in o) {
                            var b = (JObject)t.Value;
                            if (b == null) continue;
                            ProcessPiece(b);
                            if (childrenKey != null) {
                                ProcessPieces(b[childrenKey]);
                            }
                        }
                    }

                    var content = DetailsContentJObject.DeepClone();
                    ProcessPieces(content[@"cars"], @"skins");
                    ProcessPiece(content[@"track"] as JObject);
                    ProcessPieces(content[@"weather"]);
                    result.Add(PackedEntry.FromContent("cfg/cm_content/content.json", content.ToString(Formatting.Indented)));
                } else {
                    result.Add(PackedEntry.FromContent("cfg/cm_content/content.json", @"{}"));
                }
            }

            // Executable
            if (saveExecutable) {
                var serverDirectory = ServerPresetsManager.ServerDirectory;
                result.Add(PackedEntry.FromFile(
                        mode == ServerPresetPackMode.Windows ? @"acServer.exe" : @"acServer",
                        Path.Combine(serverDirectory, mode == ServerPresetPackMode.Windows ? @"acServer.exe" : @"acServer")));
            }

            // Main config file
            var serverCfg = await GetBaseMainConfigAsync();

            // Welcome message
            var welcomeMessage = BuildWelcomeMessage();
            if (welcomeMessage != null) {
                result.Add(PackedEntry.FromContent("cfg/welcome.txt", welcomeMessage));
                serverCfg["SERVER"].Set("WELCOME_MESSAGE", "cfg/welcome.txt");
                serverCfg["DATA"].Set("WELCOME_PATH", "cfg/welcome.txt");
            }

            // Setups
            var dataSection = serverCfg["DATA"];
            var setupIndex = 0;
            foreach (var key in dataSection.Keys.Where(x => x.StartsWith(@"FIXED_SETUP_")).ToList()) {
                dataSection.Remove(key);
            }
            var setupsRemap = new Dictionary<string, string>();
            foreach (var item in SetupItems) {
                if (!File.Exists(item.Filename)) continue;
                var name = $@"setups/setup_{item.CarId}_{Path.GetFileName(item.Filename)}.ini";
                result.Add(PackedEntry.FromFile(name, item.Filename));
                dataSection[@"FIXED_SETUP_" + setupIndex] = $@"{(item.IsDefault ? @"1" : @"0")}|{name}";
                setupsRemap[Path.GetFileName(item.Filename)] = Path.GetFileName(name);
                setupIndex++;
            }

            result.Add(PackedEntry.FromContent("cfg/server_cfg.ini", serverCfg.Stringify()));

            // Entry list
            var entryList = EntryListIniObject?.Clone() ?? new IniFile();
            entryList.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section, CspRequiredActual));
            foreach (var section in entryList) {
                if (section.Value.TryGetValue("FIXED_SETUP", out var setup)
                        && setupsRemap.TryGetValue(setup, out var remapped)) {
                    section.Value["FIXED_SETUP"] = remapped;
                }
            }
            result.Add(PackedEntry.FromContent("cfg/entry_list.ini", entryList.Stringify()));

            if (!DisableChecksums) {
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
                    if (!File.Exists(actualData)) continue;
                    result.Add(TrackPreprocess(actualData, out var pathPrefix, out var newContent)
                            ? PackedEntry.FromContent(Path.Combine(@"content", @"tracks", pathPrefix, localPath, @"data", file), newContent)
                            : PackedEntry.FromFile(Path.Combine(@"content", @"tracks", localPath, @"data", file), actualData));
                }

                {
                    var modelsFileName = TrackLayoutId == null ? @"models.ini" : $@"models_{TrackLayoutId}.ini";
                    var actualData = Path.Combine(AcPaths.GetTracksDirectory(root), TrackId, modelsFileName);
                    if (File.Exists(actualData)) {
                        result.Add(PackedEntry.FromFile(Path.Combine(@"content", @"tracks", TrackId, modelsFileName), actualData));
                    }
                }
            }

            // System
            var systemSurfaces = Path.Combine(ServerPresetsManager.ServerDirectory, @"system", @"data", @"surfaces.ini");
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

        private void PrepareTrack([NotNull] string trackId, [CanBeNull] string configurationId) {
            var root = AcRootDirectory.Instance.RequireValue;
            var localPath = configurationId != null ? Path.Combine(trackId, configurationId) : trackId;

            foreach (var file in TrackDataToKeep) {
                var actualData = new FileInfo(Path.Combine(AcPaths.GetTracksDirectory(root), localPath, @"data", file));
                if (!actualData.Exists) continue;
                if (TrackPreprocess(actualData.FullName, out var pathPrefix, out var newContent)) {
                    var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"tracks", pathPrefix, localPath, @"data", file));
                    Directory.CreateDirectory(serverData.DirectoryName ?? "");
                    File.WriteAllText(serverData.FullName, newContent);
                } else {
                    var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"tracks", localPath, @"data", file));
                    if (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime) {
                        Directory.CreateDirectory(serverData.DirectoryName ?? "");
                        FileUtils.HardLinkOrCopy(actualData.FullName, serverData.FullName, true);
                    }
                }
            }

            {
                var modelsFileName = TrackLayoutId == null ? @"models.ini" : $@"models_{TrackLayoutId}.ini";
                var actualData = new FileInfo(Path.Combine(AcPaths.GetTracksDirectory(root), TrackId, modelsFileName));
                if (actualData.Exists) {
                    var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"tracks", TrackId, modelsFileName));
                    if (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime) {
                        Directory.CreateDirectory(serverData.DirectoryName ?? "");
                        FileUtils.HardLinkOrCopy(actualData.FullName, serverData.FullName, true);
                    }
                }
            }
        }

        /// <summary>
        /// Update data in server’s folder according to actual data.
        /// </summary>
        private async Task PrepareServer(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            for (var i = 0; i < CarIds.Length; i++) {
                var carId = CarIds[i];
                progress?.Report(new AsyncProgressEntry(carId, i, CarIds.Length + 1));
                PrepareCar(carId);

                await Task.Yield();
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
            get => _inviteCommand;
            set => Apply(value, ref _inviteCommand);
        }

        public enum LogMessageType {
            Debug = '…',
            Message = '>',
            Warning = '‽',
            Error = '▲',
            Info = '→'
        }

        [CanBeNull]
        private string GetServerLogFilename() {
            if (!SettingsHolder.Online.ServerLogsSave) {
                return null;
            }

            var name = FileUtils.EnsureFileNameIsValid($"{Name ?? "server"}_{DateTime.Now:yyMMdd_HHmmss}.log", true);
            var directory = string.IsNullOrWhiteSpace(SettingsHolder.Online.ServerLogsDirectory)
                    ? FilesStorage.Instance.GetTemporaryDirectory("Server Logs")
                    : SettingsHolder.Online.ServerLogsDirectory;
            var keepLogsFor = SettingsHolder.Online.ServerKeepLogsDuration.TimeSpan;
            if (keepLogsFor > TimeSpan.Zero) {
                Task.Run(() => {
                    try {
                        var files = new DirectoryInfo(directory).GetFiles("*.log")
                                .Where(x => DateTime.Now - x.LastWriteTime > keepLogsFor).Select(x => x.FullName).ToArray();
                        if (files.Length > 0) {
                            Logging.Debug("Removing old log files: " + files.JoinToString("\n\t"));
                            FileUtils.Recycle(files);
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }).Ignore();
            }
            return FileUtils.EnsureUnique(Path.Combine(directory, name));
        }

        /// <summary>
        /// Start server (all stdout stuff will end up in RunningLog).
        /// </summary>
        /// <exception cref="InformativeException">For some predictable errors.</exception>
        /// <exception cref="Exception">Process starting might cause loads of problems.</exception>
        public async Task RunServer(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
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
                await SaveCommand.ExecuteAsync();
            }

            if (SettingsHolder.Online.ServerPresetsUpdateDataAutomatically
                    && !DisableChecksums) {
                await PrepareServer(progress, cancellation);
            }

            if (SettingsHolder.Online.ServerCopyConfigsToCfgFolder) {
                var root = AcRootDirectory.Instance.RequireValue;
                FileUtils.EnsureDirectoryExists(Path.Combine(root, @"server", @"cfg"));
                FileUtils.EnsureDirectoryExists(Path.Combine(root, @"server", @"setups"));

                // Main config file
                var serverCfg = await GetBaseMainConfigAsync();

                // Welcome message
                var welcomeMessage = BuildWelcomeMessage();
                if (welcomeMessage != null) {
                    File.WriteAllText(Path.Combine(root, @"server", @"cfg", @"welcome.txt"), welcomeMessage);
                    serverCfg["SERVER"].Set("WELCOME_MESSAGE", "cfg/welcome.txt");
                    serverCfg["DATA"].Set("WELCOME_PATH", "cfg/welcome.txt");
                }

                File.WriteAllText(Path.Combine(root, @"server", @"cfg", @"server_cfg.ini"), serverCfg.Stringify());

                var entryList = EntryListIniObject?.Clone() ?? new IniFile();
                entryList.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section, CspRequiredActual));
                File.WriteAllText(Path.Combine(root, @"server", @"cfg", @"entry_list.ini"), entryList.Stringify());
            }

            if (SetupItems.Count > 0) {
                var root = AcRootDirectory.Instance.RequireValue;
                FileUtils.EnsureDirectoryExists(Path.Combine(root, @"server", @"setups"));
                foreach (var item in SetupItems) {
                    File.Copy(item.Filename, Path.Combine(root, @"server", @"setups", Path.GetFileName(item.Filename)));
                }
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
            _logFilename = GetServerLogFilename();
            _logDroppingFastRate = false;
            _logDroppingCount = false;
            if (_localQueue == null) _localQueue = new List<Tuple<LogMessageType, string>>();
            else _localQueue.Clear();

            void LogAction(LogMessageType type, string msg) {
                if (msg?.Contains("\t\t$CSP0:") != false) {
                    return;
                }
                if (_localQueue.Count > 200) {
                    if (!_logDroppingFastRate) {
                        _logDroppingFastRate = true;
                        _localQueue.Add(Tuple.Create(LogMessageType.Error, "Too many messages at once, some messages are dropped"));
                        if (_localQueue.Any(x => x.Item2.EndsWith(@"Error listening %!e(syscall.Errno=536870951)"))) {
                            _localQueue.Add(Tuple.Create(LogMessageType.Error, "HTTP port is busy"));
                            _running?.Kill();
                        }
                    }
                    return;
                }
                lock (_localQueue) {
                    _localQueue.Add(Tuple.Create(type, msg));
                }
            }

            RunningLog = log;

            if (ProvideDetails && DetailsMode == ServerPresetDetailsMode.ViaWrapper) {
                await RunWrapper(serverExecutable, LogAction, progress, cancellation);
            } else {
                await RunAcServer(serverExecutable, LogAction, progress, cancellation);
            }
        }

        private string _logFilename;
        private List<Tuple<LogMessageType, string>> _localQueue;
        private bool _logDroppingFastRate;
        private bool _logDroppingCount;

        private async Task LaunchRunningUpdateAsync(Process process) {
            while (process == _running) {
                await Task.Delay(50);
                if (_localQueue.Count > 0) {
                    if (_logDroppingCount) {
                        lock (_localQueue) {
                            _localQueue.Clear();
                        }
                        continue;
                    }
                    using (var stream = _logFilename != null ? new FileStream(_logFilename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite) : null)
                    using (var writer = stream != null ? new StreamWriter(stream, Encoding.UTF8, 1024, false) : null) {
                        List<Tuple<LogMessageType, string>> items;
                        lock (_localQueue) {
                            items = _localQueue.ToList();
                            _localQueue.Clear();
                        }

                        if (RunningLog?.Count > 100000) {
                            if (!_logDroppingCount) {
                                _logDroppingCount = true;
                                items.Add(Tuple.Create(LogMessageType.Error, "Too many messages, subsequent messages are dropped"));
                            }
                            return;
                        }

                        foreach (var item in items) {
                            var type = item.Item1;
                            var msg = item.Item2;

                            if (type == LogMessageType.Message
                                    && (msg.StartsWith(@"Warning", StringComparison.OrdinalIgnoreCase)
                                            || msg.StartsWith(@"RESPONSE: ERROR", StringComparison.OrdinalIgnoreCase))) {
                                type = LogMessageType.Warning;
                            }

                            var t = DateTime.Now;
                            var prepared = $@"{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}.{t.Millisecond:D3}: {(char)type} {msg.Replace("\n", "\n  ")}";
                            RunningLog?.Add(prepared);
                            if (writer != null) {
                                await writer.WriteLineAsync(SettingsHolder.Online.ServerLogsCmFormat ? prepared : msg);
                            }
                        }
                    }
                }
            }

            var log = RunningLog;
            if (log == null) return;

            if (log.Any(x => x.EndsWith(@"LOBBY COULD NOT BE RACHED, SHUTTING SERVER DOWN")) && log.Any(x => x.EndsWith(@"CHECK YOUR PORT FORWARDING SETTINGS"))) {
                MessageDialog.Show("It seems like the lobby couldn’t reach your server to verify if it’s accessible or not. Possible solutions:\n"
                        + "• Make sure you have a public IP address (some ISPs sell it as an extra option);\n"
                        + "• Verify ports forwarding parameters in your router settings (usually you can find them [url=\"http://192.168.1.1\"]here[/url]);\n"
                        + $"• Add TCP ({HttpPort}, {TcpPort}) and UDP ({UdpPort}) ports in Windows Firewall exceptions.\n\n"
                        + "Alternatively, for a private server you can try services like Hamachi or Radmin to set a LAN Assetto Corsa server "
                        + "(make sure to uncheck “Make server public (show on lobby)” option for it to work).",
                        "Failed to register server to the lobby", MessageDialogButton.OK, @".serverFailure.lobby");
            } else if (log.Any(x => x.EndsWith(@"invalid memory address or nil pointer dereference")) && log.Any(x => x.Contains(@".ReadFromUDP("))
                    || log.Any(x => x.EndsWith(@"HTTP port is busy"))) {
                try {
                    var conflicting = GetOpenPorts().Where(port => port.Item1 ? port.Item2 == TcpPort || port.Item2 == HttpPort : port.Item2 == UdpPort)
                            .Select(x => x.Item3).Distinct().ToList();
                    if (conflicting.Count > 0) {
                        var takenBy = conflicting.Count == 1
                                ? $"a different process ({GetProcessName(conflicting[0])})"
                                : $"different processes ({conflicting.Select(GetProcessName).JoinToReadableString()})";
                        if (MessageDialog.Show($"Selected ports are already taken by {takenBy}. Kill those processes and try again?",
                                "Failed to start a server", MessageDialogButton.YesNo, @".serverFailure.port") == MessageBoxResult.Yes) {
                            await conflicting.Select(x => ProcessExtension.Start("taskkill", new[] { "/F", "/PID", x.ToInvariantString() }).WaitForExitAsync())
                                    .WhenAll();
                            await Task.Delay(500);
                            RunServerCommand.ExecuteAsync().Ignore();
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        private static string GetProcessName(int pid) {
            try {
                using (var p = Process.GetProcessById(pid)) {
                    return Path.GetFileName(p.GetFilenameSafe() ?? $@"{p.ProcessName}.exe");
                }
            } catch {
                return @"unknown";
            }
        }

        private static IEnumerable<Tuple<bool, int, int>> GetOpenPorts() {
            using (var proc = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "netstat.exe",
                    Arguments = "-a -n -o",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            }) {
                proc.Start();

                var ret = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0) {
                    throw new Exception("Failed to start netstart.exe");
                }

                int protoIndex = -1, addressIndex = -1, pidIndex = -1;
                foreach (var tokens in ret.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => Regex.Split(x.Trim(), "\\s{2,}")).Where(x => x.Length > 2)) {
                    if (protoIndex == -1 || addressIndex == -1 || pidIndex == -1) {
                        protoIndex = tokens.FindIndex(t => t.IndexOf(@"proto", StringComparison.OrdinalIgnoreCase) != -1);
                        addressIndex = tokens.FindIndex(t => t.IndexOf(@"local", StringComparison.OrdinalIgnoreCase) != -1);
                        pidIndex = tokens.FindIndex(t => t.IndexOf(@"pid", StringComparison.OrdinalIgnoreCase) != -1);
                    } else {
                        yield return Tuple.Create(tokens[protoIndex] == @"TCP",
                                tokens[addressIndex].Substring(tokens[addressIndex].LastIndexOf(':') + 1).As(0),
                                tokens[Math.Min(pidIndex, tokens.Length - 1)].As(0));
                    }
                }
            }
        }

        [ItemCanBeNull]
        private static async Task<string> LoadWinWrapper(CancellationToken cancellation) {
            var wrapperFilename = FilesStorage.Instance.GetFilename("Server Wrapper", "AcServerWrapper.exe");

            var data = await CmApiProvider.GetStaticDataAsync("ac_server_wrapper", TimeSpan.Zero, cancellation: cancellation);
            if (cancellation.IsCancellationRequested || data == null) return null;

            if (data.Item2) {
                // Freshly loaded
                var wrapperBytes = await FileUtils.ReadAllBytesAsync(data.Item1);
                if (cancellation.IsCancellationRequested) return null;

                await Task.Run(() => {
                    using (var stream = new MemoryStream(wrapperBytes, false))
                    using (var archive = new ZipArchive(stream)) {
                        try {
                            var entry = archive.GetEntry("acServerWrapper.exe");
                            if (entry != null) {
                                File.WriteAllBytes(wrapperFilename, entry.Open().ReadAsBytesAndDispose());
                            }
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                });
            }

            return File.Exists(wrapperFilename) ? wrapperFilename : null;
        }

        [ItemCanBeNull]
        private static async Task<string> LoadLinux32Wrapper(CancellationToken cancellation) {
            return (await CmApiProvider.GetStaticDataAsync("ac_server_wrapper-linux-x86", TimeSpan.Zero, cancellation: cancellation))?.Item1;
        }

        [ItemCanBeNull]
        private static async Task<string> LoadLinux64Wrapper(CancellationToken cancellation) {
            return (await CmApiProvider.GetStaticDataAsync("ac_server_wrapper-linux-x64", TimeSpan.Zero, cancellation: cancellation))?.Item1;
        }

        private class HideContent : IDisposable {
            private List<Tuple<string, string>> _moved = new List<Tuple<string, string>>();

            public HideContent(string serverExecutable) {
                var directory = Path.GetDirectoryName(serverExecutable);
                if (string.IsNullOrEmpty(directory)) return;
                Move(Path.Combine(directory, "content"));
            }

            private void Move(string directory) {
                var newName = FileUtils.EnsureUnique(directory + "~tmp");
                try {
                    Directory.Move(directory, newName);
                    _moved.Add(Tuple.Create(newName, directory));
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            public void Dispose() {
                lock (_moved) {
                    foreach (var tuple in _moved) {
                        try {
                            Directory.Move(tuple.Item1, tuple.Item2);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                    _moved.Clear();
                }
            }
        }

        Process StartServer(string serverExecutable, Func<Process> callback) {
            if (!DisableChecksums) {
                return callback();
            }

            var hidden = new HideContent(serverExecutable);
            Task.Delay(TimeSpan.FromSeconds(3d)).ContinueWith(r => hidden.Dispose());
            var ret = callback();
            ret.Exited += (sender, args) => hidden.Dispose();
            ret.Disposed += (sender, args) => hidden.Dispose();
            return ret;
        }

        private async Task RunWrapper(string serverExecutable, Action<LogMessageType, string> log, [CanBeNull] IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Loading wrapper…"));
            var wrapperFilename = await LoadWinWrapper(cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (wrapperFilename == null) {
                throw new InformativeException("Can’t run server", "Can’t load server wrapper.");
            }

            try {
                var now = DateTime.Now;
                var logName = FileUtils.EnsureFileNameIsValid(
                        $"Server_{DisplayName}_{now.Year % 100:D2}{now.Month:D2}{now.Day:D2}_{now.Hour:D2}{now.Minute:D2}{now.Second:D2}.log", true);
                using (var writer = new StreamWriter(FilesStorage.Instance.GetFilename("Logs", logName), false))
                using (var process = StartServer(serverExecutable, () => ProcessExtension.Start(wrapperFilename, new[] {
                    "-e", serverExecutable, $"presets/{Id}"
                }, new ProcessStartInfo {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(serverExecutable) ?? "",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }))) {
                    // process.Start(); // why?
                    SetRunning(process);
                    ChildProcessTracker.AddProcess(process);

                    progress?.Report(AsyncProgressEntry.Finished);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.OutputDataReceived += (sender, args) => {
                        if (!string.IsNullOrWhiteSpace(args.Data)) {
                            writer.WriteLine(args.Data);
                            log(LogMessageType.Message, args.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) => {
                        if (!string.IsNullOrWhiteSpace(args.Data)) {
                            writer.WriteLine(args.Data);
                            log(LogMessageType.Error, args.Data);
                        }
                    };

                    if (!process.HasExitedSafe()) {
                        await process.WaitForExitAsync(cancellation);
                    }

                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }

                    log(LogMessageType.Info, $"Stopped: {process.ExitCode}");
                }
            } finally {
                SetRunning(null);
            }
        }

        private AcServerPluginManager _pluginManager;

        private CmServerPlugin _cmPlugin;

        [CanBeNull]
        public CmServerPlugin CmPlugin {
            get => _cmPlugin;
            set => Apply(value, ref _cmPlugin);
        }

        private async Task RunAcServer(string serverExecutable, Action<LogMessageType, string> log, IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation) {
            Process process = null;
            try {
                process = StartServer(serverExecutable, () => new Process {
                    StartInfo = {
                        FileName = serverExecutable,
                        Arguments = $@"-c ""{IniFilename}"" -e ""{EntryListIniFilename}""",
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(serverExecutable) ?? "",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    }
                });

                process.Start();
                SetRunning(process);
                ChildProcessTracker.AddProcess(process);
                progress?.Report(AsyncProgressEntry.Finished);

                if (UseCmPlugin) {
                    DisposeHelper.Dispose(ref _pluginManager);
                    _pluginManager = new AcServerPluginManager(new AcServerPluginManagerSettings(this) { LogServerRequests = false });
                    foreach (var entry in PluginEntries.Where(x => !string.IsNullOrWhiteSpace(x.Address) && x.UdpPort > 0 && x.UdpPort < 65536)) {
                        var parseAddress = Regex.Match(entry.Address, @"^([^:/\\]+):(\d+)$");
                        if (parseAddress.Success) {
                            _pluginManager.AddExternalPlugin(new ExternalPluginInfo(entry.UdpPort ?? 0,
                                    parseAddress.Groups[1].Value, parseAddress.Groups[2].Value.As(0)));
                        }
                    }
                    _pluginManager.AddPlugin(CmPlugin = new CmServerPlugin(log, Capacity));
                    if (CmPluginLiveConditions) {
                        var track = TrackId == null ? null : await TracksManager.Instance.GetLayoutByIdAsync(TrackId, TrackLayoutId);
                        _pluginManager.AddPlugin(new LiveConditionsServerPlugin(track, RequiredCspVersion >= 1643, CmPluginLiveConditionsParams.Clone()));
                    }

                    // _pluginManager.AddPlugin(new DynamicConditionsV2ServerPlugin());
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.OutputDataReceived += (sender, args) => log(LogMessageType.Message, args.Data);
                process.ErrorDataReceived += (sender, args) => log(LogMessageType.Error, args.Data);

                if (!process.HasExitedSafe()) {
                    await process.WaitForExitAsync(cancellation);
                }

                if (!process.HasExitedSafe()) {
                    process.Kill();
                }

                log(LogMessageType.Info, $"Stopped: {process.ExitCode}");
            } catch (TaskCanceledException) {
                StopServer();
            } finally {
                SetRunning(null);
                process?.Dispose();
                CmPlugin = null;
                DisposeHelper.Dispose(ref _pluginManager);
            }
        }

        private Process _running;

        private void SetRunning(Process running) {
            _running = running;
            OnPropertyChanged(nameof(IsRunning));
            _stopServerCommand?.RaiseCanExecuteChanged();
            _runServerCommand?.RaiseCanExecuteChanged();
            _restartServerCommand?.RaiseCanExecuteChanged();

            DisposeHelper.Dispose(ref _pluginManager);
            CmPlugin = null;
            LaunchRunningUpdateAsync(running).Ignore();
        }

        public override void Reload() {
            if (IsRunning) {
                try {
                    _running.Kill();
                    _running = null;
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            base.Reload();
        }

        public bool IsRunning => _running != null;

        private BetterObservableCollection<string> _runningLog;

        public BetterObservableCollection<string> RunningLog {
            get => _runningLog;
            set => Apply(value, ref _runningLog);
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