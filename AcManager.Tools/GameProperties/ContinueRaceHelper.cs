using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class ContinueRaceHelper : Game.GameHandler {
        public static bool ContinueRace() {
            try {
                if (AcSharedMemory.Instance.IsPaused) {
                    AcMousePretender.ClickContinueButton();
                    return true;
                }
            } catch (Exception ex) {
                Logging.Error(ex);
            }

            return false;
        }

        private class MemoryListener : IDisposable {
            private KeyboardListener _keyboard;

            public MemoryListener() {
                try {
                    _keyboard = new KeyboardListener();
                    _keyboard.PreviewKeyDown += OnKeyDown;
                    _keyboard.PreviewKeyUp += OnKeyUp;
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            private static bool _isKeyDown;

            private static void OnKeyDown(object sender, VirtualKeyCodeEventArgs e) {
                if (!_isKeyDown && e.Key == Keys.Escape && Keyboard.Modifiers == ModifierKeys.None) {
                    _isKeyDown = true;
                    e.Handled |= ContinueRace();
                }
            }

            private static void OnKeyUp(object sender, VirtualKeyCodeEventArgs e) {
                if (e.Key == Keys.Escape) {
                    _isKeyDown = false;
                }
            }

            public void Dispose() {
                try {
                    DisposeHelper.Dispose(ref _keyboard);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t remove events hook", e);
                }
            }
        }

        public override IDisposable Set(Process process) {
            return new MemoryListener();
        }
    }
}