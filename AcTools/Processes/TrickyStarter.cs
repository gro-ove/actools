using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Properties;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.Processes {
    public class TrickyStarter : IAcsStarter {
        private const long ApproximateAcstarterSize = 50000;
        private const int WaitIter = 200;

        public readonly string AcRoot;

        public TrickyStarter(string acRoot) {
            AcRoot = acRoot;
        }
        public bool Use32Version { get; set; }

        protected string AcsName => Use32Version ? "acs_x86.exe" : "acs.exe";

        private string _acLauncher;
        private string _acLauncherBackup;
        private Process _launcherProcess, _gameProcess;

        public void Run() {
            SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false);

            _acLauncher = FileUtils.GetAcLauncherFilename(AcRoot);
            _acLauncherBackup = _acLauncher.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase) + "_backup_ts.exe";

            if (File.Exists(_acLauncherBackup) && new FileInfo(_acLauncher).Length > ApproximateAcstarterSize) {
                File.Move(_acLauncherBackup, _acLauncherBackup.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase) + "_" + DateTime.Now.Ticks + ".exe");
            }

            if (!File.Exists(_acLauncherBackup)) {
                File.Move(_acLauncher, _acLauncherBackup);
            }

            if (!File.Exists(_acLauncher)) {
                File.WriteAllBytes(_acLauncher, Resources.AcStarter);
            }

            _launcherProcess = Process.Start(new ProcessStartInfo {
                WorkingDirectory = AcRoot,
                FileName = Path.GetFileName(_acLauncher),
                Arguments = $"--first-stage {AcsName}"
            });
        }

        public void WaitUntilGame() {
            _launcherProcess?.WaitForExit();

            for (var i = 0; i < 10; i++) {
                _gameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (_gameProcess != null) break;
                Thread.Sleep(2500);
            }

            Thread.Sleep(2500);
            _gameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public void WaitGame() {
            _gameProcess?.WaitForExit();
        }

        public void CleanUp() {
            if (_gameProcess != null && !_gameProcess.HasExitedSafe()) {
                try {
                    _gameProcess.Kill();
                } catch (Exception) {
                    // ignored
                }
            }

            _gameProcess?.Dispose();
            _gameProcess = null;

            if (_acLauncher == null || new FileInfo(_acLauncher).Length > ApproximateAcstarterSize) return;

            Thread.Sleep(200);

            var isRestored = false;
            for (var i = 0; i < 10 && File.Exists(_acLauncherBackup); i++) {
                try {
                    if (!isRestored) {
                        if (File.Exists(_acLauncher)) {
                            File.Delete(_acLauncher);
                        }
                        File.Copy(_acLauncherBackup, _acLauncher);
                        isRestored = true;
                    }

                    File.Delete(_acLauncherBackup);
                } catch (IOException) { }

                Thread.Sleep(WaitIter);
            }

            if (File.Exists(_acLauncherBackup)) {
                throw new Exception("Cannot restore original AssettoCorsa.exe.");
            }
        }

        public async Task RunAsync(CancellationToken cancellation) {
            await Task.Run(() => Run(), cancellation);
        }

        public async Task<Process> WaitUntilGameAsync(CancellationToken cancellation) {
            if (_launcherProcess != null) {
                await _launcherProcess.WaitForExitAsync(cancellation);
                _launcherProcess.Dispose();
                _launcherProcess = null;
            }

            try {
                for (var i = 0; i < 500; i++) {
                    _gameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (_gameProcess != null) break;
                    await Task.Delay(50, cancellation);
                    if (cancellation.IsCancellationRequested) return null;
                }
            } catch (Exception e) when (e.IsCanceled()) {
                return null;
            }

            // we have to wait again — first process might be “fake”
            await Task.Delay(2500, cancellation);
            _gameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            return _gameProcess;
        }

        public async Task WaitGameAsync(CancellationToken cancellation) {
            if (_gameProcess == null) return;
            await _gameProcess.WaitForExitAsync(cancellation);
        }

        public async Task CleanUpAsync(CancellationToken cancellation) {
            if (_gameProcess != null && !_gameProcess.HasExitedSafe()) {
                try {
                    _gameProcess.Kill();
                } catch (Exception) {
                    // ignored
                }
            }

            _gameProcess?.Dispose();
            _gameProcess = null;

            if (_acLauncher == null || new FileInfo(_acLauncher).Length > ApproximateAcstarterSize) return;

            await Task.Delay(200, cancellation);
            if (cancellation.IsCancellationRequested) return;

            var isRestored = false;
            for (var i = 0; i < 10 && File.Exists(_acLauncherBackup); i++) {
                try {
                    if (!isRestored) {
                        if (File.Exists(_acLauncher)) {
                            File.Delete(_acLauncher);
                        }
                        File.Copy(_acLauncherBackup, _acLauncher);
                        isRestored = true;
                    }

                    File.Delete(_acLauncherBackup);
                } catch (IOException) { }

                await Task.Delay(WaitIter, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }

            if (File.Exists(_acLauncherBackup)) {
                throw new Exception("Cannot restore original AssettoCorsa.exe");
            }
        }

        public bool RunSteamIfNeeded { get; set; }
    }
}
