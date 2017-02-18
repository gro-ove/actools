using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Windows;

namespace AcManager.Tools.GameProperties {
    public class ImmediateStart : Game.GameHandler {
        private bool _cancelled;

        public override IDisposable Set() {
            if (SettingsHolder.Drive.WatchForSharedMemory) {
                return SetSharedListener();
            }

            RunDelayed().Forget();
            return new ActionAsDisposable(() => _cancelled = true);
        }

        private static bool IsAcWindowActive() {
            return AcSharedMemory.TryToFindGameProcess()?.MainWindowHandle == User32.GetForegroundWindow();
        }

        private static async Task PeriodicChecks(bool[] cancellation) {
            while (!cancellation[0]) {
                await Task.Delay(1000);
                if (IsAcWindowActive()) {
                    Run();
                }
            }
        }

        private IDisposable SetSharedListener() {
            var cancellation = new[] { false };

            EventHandler handler = null;
            handler = (sender, args) => {
                if (IsAcWindowActive()) {
                    Run();
                }

                cancellation[0] = true;
                AcSharedMemory.Instance.Start -= handler;
            };

            AcSharedMemory.Instance.Start += handler;
            PeriodicChecks(cancellation).Forget();

            return new ActionAsDisposable(() => {
                cancellation[0] = true;
                AcSharedMemory.Instance.Start -= handler;
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