using System;
using System.Diagnostics;
using System.Windows.Forms;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class ContinueRaceHelper : Game.GameHandler {
        private class MemoryListener : IDisposable {
            private KeyboardListener _keyboard;

            public MemoryListener() {
                try {
                    _keyboard = new KeyboardListener();
                    _keyboard.KeyUp += OnKey;
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            private void OnKey(object sender, KeyEventArgs e) {
                try {
                    if (e.KeyCode == Keys.Escape && !e.Control && !e.Shift && !e.Alt && AcSharedMemory.Instance.IsPaused &&
                            (DateTime.Now - AcSharedMemory.Instance.PauseTime).TotalSeconds > 0.15) {
                        AcMousePretender.ClickContinueButton();
                    }
                } catch (Exception ex) {
                    Logging.Error(ex);
                }
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _keyboard);
            }
        }

        public override IDisposable Set(Process process) {
            return new MemoryListener();
        }
    }
}