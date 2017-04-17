using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Render.Temporary;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

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
            private KeyboardListener _keyboard;
            private readonly CancellationTokenSource _sharedCancellationTokenSource;

            public MemoryListener(CancellationTokenSource sharedCancellationTokenSource) {
                _sharedCancellationTokenSource = sharedCancellationTokenSource;

                try {
                    _keyboard = new KeyboardListener();
                    _keyboard.KeyUp += OnKeyUp;
                } catch (Exception e) {
                    Logging.Error(e);
                }

                AcSharedMemory.Instance.Start += OnStart;
            }

            private void OnStart(object sender, EventArgs e) {
                Dispose();
            }

            private void OnKeyUp(object sender, KeyEventArgs e) {
                try {
                    if (e.KeyCode == Keys.Escape && !e.Control && !e.Shift && !e.Alt) {
                        Logging.Write("Escape was pressed, terminating loading…");
                        _sharedCancellationTokenSource?.Cancel();
                    }
                } catch (Exception ex) {
                    Logging.Error(ex);
                }
            }

            public void Dispose() {
                // do not dispose _sharedCancellationTokenSource cause it’s shared

                DisposeHelper.Dispose(ref _keyboard);
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