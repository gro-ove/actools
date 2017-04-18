using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Starters {
    public abstract class StarterBase : IAcsPlatformSpecificStarter {
        public bool Use32Version { get; set; }

        protected Process LauncherProcess, GameProcess;

        protected virtual string AcsName => Use32Version ? "acs_x86.exe" : "acs.exe";

        protected string AcsFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, AcsName);
        
        public abstract void Run();

        protected StarterBase() {
            PrepareEnvironment();
        }

        [CanBeNull]
        private Process TryToGetGameProcess() {
            return Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public void WaitUntilGame() {
            if (GameProcess != null) return;

            for (var i = 0; i < 100; i++) {
                GameProcess = TryToGetGameProcess();
                if (GameProcess != null) break;
                Thread.Sleep(500);
            }
        }

        protected void SetAcX86Param() {
            IniFile.Write(Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "launcher.ini"), "WINDOW", "X86", Use32Version ? @"1" : @"0");
        }

        public virtual void WaitGame() {
            GameProcess?.WaitForExit();
        }

        public virtual void CleanUp() {
            if (LauncherProcess != null) {
                LauncherProcess.Dispose();
                LauncherProcess = null;
            }

            if (GameProcess == null) {
                GameProcess = TryToGetGameProcess();
            }

            if (GameProcess != null) {
                try {
                    if (!GameProcess.HasExitedSafe()) {
                        GameProcess.Kill();
                    }
                } catch (Exception e) {
                    Logging.Warning("Process killing exception: " + e);
                }

                GameProcess.Dispose();
                GameProcess = null;
            }
        }

        public virtual Task RunAsync(CancellationToken cancellation) {
            return Task.Run(() => Run(), cancellation);
        }

        private static bool IsAny() {
            return Process.GetProcessesByName("acs.exe").Any() || Process.GetProcessesByName("acs_x86.exe").Any() ||
                    Process.GetProcessesByName("AssettoCorsa.exe").Any();
        }

        public async Task<Process> WaitUntilGameAsync(CancellationToken cancellation) {
            if (GameProcess != null) {
                Logging.Debug("game is already here!");
                return GameProcess;
            }

            Logging.Debug("waiting for game…");

            try {
                var nothing = 0;
                for (var i = 0; i < 9999; i++) {
                    GameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (GameProcess != null) break;

                    if (IsAny()) {
                        nothing = 0;
                    } else if (++nothing > 500) {
                        break;
                    }

                    await Task.Delay(100, cancellation);
                    if (cancellation.IsCancellationRequested) return null;
                }
            } catch (TaskCanceledException) {
                return null;
            }

            Logging.Debug("here it is!");
            return GameProcess;
        }

        protected void PrepareEnvironment() {
            Environment.SetEnvironmentVariable("PYTHONHOME", string.Empty);
            Environment.SetEnvironmentVariable("PYTHONOPTIMIZE", string.Empty);
            Environment.SetEnvironmentVariable("PYTHONPATH", string.Empty);
        }

        public virtual async Task WaitGameAsync(CancellationToken cancellation) {
            if (GameProcess == null) return;
            await GameProcess.WaitForExitAsync(cancellation);
        }

        public virtual async Task CleanUpAsync(CancellationToken cancellation) {
            await Task.Run(() => CleanUp(), cancellation);
        }

        public bool RunSteamIfNeeded { get; set; }
    }
}