using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Starters {
    public abstract class BaseStarter : IAcsPlatformSpecificStarter {
        public bool Use32Version { get; set; }

        protected Process LauncherProcess, GameProcess;

        protected string AcsName => Use32Version ? "acs_x86.exe" : "acs.exe";

        protected string AcsFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, AcsName);

        public abstract void Run();

        public void WaitUntilGame() {
            if (GameProcess != null) return;

            for (var i = 0; i < 100; i++) {
                GameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (GameProcess != null) break;
                Thread.Sleep(1000);
            }

            Thread.Sleep(1000);
            GameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public virtual void WaitGame() {
            GameProcess?.WaitForExit();
        }

        public virtual void CleanUp() {
            if (LauncherProcess != null) {
                LauncherProcess.Dispose();
                LauncherProcess = null;
            }

            if (GameProcess != null) {
                try {
                    if (!GameProcess.HasExited) {
                        GameProcess.Kill();
                    }
                } catch (Exception e) {
                    Logging.Warning("[BASESTARTER] Process killing exception: " + e);
                }

                GameProcess.Dispose();
                GameProcess = null;
            }
        }

        public virtual async Task RunAsync(CancellationToken cancellation) {
            await Task.Run(() => Run(), cancellation);
        }

        public async Task WaitUntilGameAsync(CancellationToken cancellation) {
            if (GameProcess != null) return;

            Logging.Warning("[BASESTARTER] WaitUntilGameAsync(): first stage");

            for (var i = 0; i < 100; i++) {
                GameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (GameProcess != null) break;
                Logging.Warning($"[BASESTARTER] ActiveProcess '{AcsName}' == null…");
                await Task.Delay(1000, cancellation);
            }

            Logging.Warning("[BASESTARTER] WaitUntilGameAsync(): second stage");

            await Task.Delay(1000, cancellation);
            GameProcess = Process.GetProcessesByName(AcsName.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public virtual async Task WaitGameAsync(CancellationToken cancellation) {
            if (GameProcess == null) return;
            await GameProcess.WaitForExitAsync(cancellation);
        }

        public virtual async Task CleanUpAsync(CancellationToken cancellation) {
            await Task.Run(() => CleanUp(), cancellation);
        }
    }
}