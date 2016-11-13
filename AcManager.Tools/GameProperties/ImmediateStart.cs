using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;

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

        private IDisposable SetSharedListener() {
            EventHandler handler = null;

            handler = (sender, args) => {
                Run();
                AcSharedMemory.Instance.Start -= handler;
            };

            AcSharedMemory.Instance.Start += handler;

            return new ActionAsDisposable(() => {
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