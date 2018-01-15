using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;

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
                _process = AcProcess.TryToFind();
                if (_process == null) return false;
            }

            return _process.MainWindowHandle == User32.GetForegroundWindow();
        }

        /*private async Task PeriodicChecks() {
            while (!_cancelled) {
                await Task.Delay(1000);
                if (IsAcWindowActive()) {
                    Run(false);
                }
            }
        }*/

        private IDisposable SetSharedListener() {
            async void Handler(object sender, EventArgs args) {
                _cancelled = true;
                DisposeHelper.Dispose(ref _process);
                AcSharedMemory.Instance.Start -= Handler;

                for (var i = 0; i < 20; i++) {
                    Run(true);

                    var isInPit = AcSharedMemory.Instance.Shared?.Graphics.IsInPits;
                    if (isInPit == false) {
                        Logging.Debug("Is in pit: " + isInPit);
                        break;
                    }

                    await Task.Delay(30);
                }
            }

            AcSharedMemory.Instance.Start += Handler;
            // PeriodicChecks().Forget();

            return new ActionAsDisposable(() => {
                _cancelled = true;
                DisposeHelper.Dispose(ref _process);
                AcSharedMemory.Instance.Start -= Handler;
            });
        }

        private bool _ran;
        private static bool _socketFailed;

        private void Run(bool allowCmd) {
            if (_ran) return;

            if (!_socketFailed && allowCmd) {
                if (InternalUtils.AcControlPointExecute(InternalUtils.AcControlPointCommand.StartGame)) {
                    _ran = true;
                    return;
                }

                _socketFailed = true;
            }

            if (IsAcWindowActive()) {
                AcMousePretender.ClickStartButton();
            }
        }

        private async Task RunDelayed() {
            await Task.Delay(5000);
            if (_cancelled) return;

            Run(false);
        }
    }
}