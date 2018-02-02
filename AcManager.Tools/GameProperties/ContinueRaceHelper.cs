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
            private ISneakyPeeky _keyboard;

            public MemoryListener() {
                try {
                    _keyboard = SneakyPeekyFactory.Get();
                    _keyboard.WatchFor(Keys.Escape);
                    _keyboard.PreviewPeek += OnPeek;
                    _keyboard.PreviewSneak += OnSneak;
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            private static bool _isKeyDown;

            private static void OnPeek(object sender, SneakyPeekyEventArgs e) {
                if (!_isKeyDown && e.SneakedPeeked == Keys.Escape && Keyboard.Modifiers == ModifierKeys.None) {
                    _isKeyDown = true;
                    e.Handled |= ContinueRace();
                }
            }

            private static void OnSneak(object sender, SneakyPeekyEventArgs e) {
                if (e.SneakedPeeked == Keys.Escape) {
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