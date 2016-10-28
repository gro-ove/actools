using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Windows.Input;

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

        public IDisposable SetSharedListener() {
            EventHandler handler = null;

            handler = (sender, args) => {
                Run();
                AcSharedMemory.Instance.GameStarted -= handler;
            };

            AcSharedMemory.Instance.GameStarted += handler;

            return new ActionAsDisposable(() => {
                AcSharedMemory.Instance.GameStarted -= handler;
            });
        }

        public static void Run() {
            var originalPosition = Cursor.Position;
            var inputSimulator = new InputSimulator();
            inputSimulator.Mouse.MoveMouseTo(65536d * 50 / Screen.PrimaryScreen.Bounds.Width,
                    65536d * 150 / Screen.PrimaryScreen.Bounds.Height);
            inputSimulator.Mouse.LeftButtonClick();
            inputSimulator.Mouse.MoveMouseTo(65536d * originalPosition.X / Screen.PrimaryScreen.Bounds.Width,
                    65536d * originalPosition.Y / Screen.PrimaryScreen.Bounds.Height);
        }

        private async Task RunDelayed() {
            await Task.Delay(5000);
            if (_cancelled) return;

            Run();
        }
    }
}