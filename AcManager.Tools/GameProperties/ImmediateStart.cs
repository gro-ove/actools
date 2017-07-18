using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using AcTools.Windows;

namespace AcManager.Tools.GameProperties {
    public class ImmediateStart : Game.GameHandler {
        private bool _cancelled;
        private Process _process;

        public override IDisposable Set(Process process) {
            if (SettingsHolder.Drive.WatchForSharedMemory) {
                return SetSharedListener();
            }

            RunDelayed().Forget();
            return new ActionAsDisposable(() => _cancelled = true);
        }

        private bool IsAcWindowActive() {
            if (_cancelled) return false;

            if (_process == null) {
                _process = AcSharedMemory.TryToFindGameProcess();
                if (_process == null) return false;
            }

            return _process.MainWindowHandle == User32.GetForegroundWindow();
        }

        private async Task PeriodicChecks() {
            while (!_cancelled) {
                await Task.Delay(1000);
                if (IsAcWindowActive()) {
                    Run();
                }
            }
        }

        private IDisposable SetSharedListener() {
            void Handler(object sender, EventArgs args) {
                if (IsAcWindowActive()) {
                    Run();
                }

                _cancelled = true;
                DisposeHelper.Dispose(ref _process);
                AcSharedMemory.Instance.Start -= Handler;
            }

            AcSharedMemory.Instance.Start += Handler;
            PeriodicChecks().Forget();

            return new ActionAsDisposable(() => {
                _cancelled = true;
                DisposeHelper.Dispose(ref _process);
                AcSharedMemory.Instance.Start -= Handler;
            });
        }

        private static void Run() {
            AcMousePretender.ClickStartButton();
        }

        private async Task RunDelayed() {
            await Task.Delay(5000);
            if (_cancelled) return;

            Run();
        }
    }
}