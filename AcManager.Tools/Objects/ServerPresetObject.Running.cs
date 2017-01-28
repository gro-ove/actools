using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.VisualBasic.Logging;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        private static void PrepareCar([NotNull] string carId) {
            var root = AcRootDirectory.Instance.RequireValue;
            var actualData = new FileInfo(Path.Combine(FileUtils.GetCarDirectory(root, carId), "data.acd"));
            var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"cars", carId, @"data.acd"));

            if (actualData.Exists && (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime)) {
                Directory.CreateDirectory(serverData.DirectoryName ?? "");
                FileUtils.Hardlink(actualData.FullName, serverData.FullName, true);
            }
        }

        private static void PrepareTrack([NotNull] string trackId, [CanBeNull] string configurationId) {
            var root = AcRootDirectory.Instance.RequireValue;

            foreach (var file in new[] {
                @"surfaces.ini", @"drs_zones.ini"
            }) {
                var localPath = configurationId != null ? Path.Combine(trackId, configurationId) : trackId;
                var actualData = new FileInfo(Path.Combine(FileUtils.GetTracksDirectory(root), localPath, @"data", file));
                var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"tracks", localPath, @"data", file));

                if (actualData.Exists && (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime)) {
                    Directory.CreateDirectory(serverData.DirectoryName ?? "");
                    FileUtils.Hardlink(actualData.FullName, serverData.FullName, true);
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

            if (SettingsHolder.Online.ServerPresetsUpdateDataAutomatically) {
                await PrepareServer(progress, cancellation);
            }

            var log = new BetterObservableCollection<string>();
            RunningLog = log;
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
