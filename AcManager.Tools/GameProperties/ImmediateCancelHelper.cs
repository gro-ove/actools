using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using KeyboardEventArgs = AcTools.Windows.Input.KeyboardEventArgs;

namespace AcManager.Tools.GameProperties {
    public class ImmediateCancelHelper : Game.GameHandler, IDisposable {
        private CancellationTokenSource _cancellationTokenSource;

        public ImmediateCancelHelper() {
            WeakEventManager<AcSharedMemory, EventArgs>.AddHandler(AcSharedMemory.Instance, nameof(AcSharedMemory.Start), OnStart);
        }

        private bool _started;

        private void OnStart(object sender, EventArgs e) {
            _started = true;
        }

        public override IDisposable Set(Process process) {
            return SettingsHolder.Drive.WatchForSharedMemory ? SetSharedListener() : null;
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _cancellationTokenSource);
        }

        public CancellationToken GetCancellationToken() {
            if (_cancellationTokenSource == null) {
                _cancellationTokenSource = new CancellationTokenSource();
            }

            return _cancellationTokenSource.Token;
        }

        private class MemoryListener : IDisposable {
            private IKeyboardListener _keyboard;
            private readonly CancellationTokenSource _sharedCancellationTokenSource;

            public MemoryListener(CancellationTokenSource sharedCancellationTokenSource) {
                _sharedCancellationTokenSource = sharedCancellationTokenSource;

                try {
                    _keyboard = KeyboardListenerFactory.Get();
                    _keyboard.WatchFor(Keys.Escape);
                    _keyboard.KeyUp += OnKeyUp;
                } catch (Exception e) {
                    Logging.Error(e);
                }

                AcSharedMemory.Instance.Start += OnStart;
            }

            private void OnStart(object sender, EventArgs e) {
                Logging.Write("Race started");
                Dispose();
            }

            private void OnKeyUp(object sender, KeyboardEventArgs e) {
                try {
                    if (e.Key == Keys.Escape && Keyboard.Modifiers == ModifierKeys.None) {
                        Logging.Write("Escape was pressed, terminating loading…");
                        _sharedCancellationTokenSource?.Cancel();
                    }
                } catch (Exception ex) {
                    Logging.Error(ex);
                }
            }

            public void Dispose() {
                // do not dispose _sharedCancellationTokenSource cause it’s shared

                try {
                    DisposeHelper.Dispose(ref _keyboard);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t remove events hook", e);
                }

                AcSharedMemory.Instance.Start -= OnStart;
            }
        }

        private IDisposable SetSharedListener() {
            if (_started) {
                return null;
            }

            if (_cancellationTokenSource == null) {
                _cancellationTokenSource = new CancellationTokenSource();
            }

            return new MemoryListener(_cancellationTokenSource);
        }
    }
}