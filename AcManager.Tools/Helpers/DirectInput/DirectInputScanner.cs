using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX.DirectInput;
// ReSharper disable MemberHidesStaticFromOuterClass

namespace AcManager.Tools.Helpers.DirectInput {
    public static class DirectInputScanner {
        public static TimeSpan OptionMinRescanPeriod = TimeSpan.FromSeconds(3d);

        private static bool _threadStarted, _isActive;
        private static readonly object ThreadSync = new object();
        private static IList<DeviceInstance> _staticData;
        private static string _staticDataFootprint;
        private static SlimDX.DirectInput.DirectInput _directInput;
        private static TimeSpan _scanTime;

        public static void Shutdown() {
            _isActive = false;
        }

        private static void StartScanning() {
            if (_threadStarted) return;
            _threadStarted = true;
            _isActive = true;
            new Thread(Scan) {
                Name = "CM Devices Scan",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            }.Start();
        }

        private static bool UpdateLists(IList<DeviceInstance> newData) {
            var footprint = newData?.Select(x => x.InstanceGuid).JoinToString(';');
            if (footprint == _staticDataFootprint) return false;

            _staticDataFootprint = footprint;
            _staticData = newData;
            lock (Instances) {
                for (var i = Instances.Count - 1; i >= 0; i--) {
                    Instances[i].RaiseUpdate(newData);
                }
            }

            return true;
        }

        private static void UpdateScanTime() {
            lock (Instances) {
                for (var i = Instances.Count - 1; i >= 0; i--) {
                    Instances[i].RaiseScanTimeUpdate();
                }
            }
        }

        private static void Scan() {
            _directInput = new SlimDX.DirectInput.DirectInput();

            try {
                while (_isActive) {
                    var getDevices = Stopwatch.StartNew();

                    IList<DeviceInstance> list;
                    try {
                        list = _directInput?.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
                    } catch (Exception e) {
                        // TODO: Try to re-initiate scanning later?
                        Logging.Error(e);
                        list = new List<DeviceInstance>(0);
                        DisposeHelper.Dispose(ref _directInput);
                    }

                    getDevices.Stop();
                    _scanTime = getDevices.Elapsed;

                    if (!UpdateLists(list)) {
                        UpdateScanTime();
                    }

                    Thread.Sleep(OptionMinRescanPeriod + getDevices.Elapsed);

                    int count;
                    lock (Instances) {
                        count = Instances.Count;
                    }

                    if (count == 0) {
                        lock (ThreadSync) {
                            Monitor.Wait(ThreadSync);
                        }
                    }
                }
            } finally {
                DisposeHelper.Dispose(ref _directInput);
            }
        }

        private static readonly List<Watcher> Instances = new List<Watcher>();

        [CanBeNull]
        public static SlimDX.DirectInput.DirectInput DirectInput => _directInput;

        [NotNull]
        public static Watcher Watch() {
            StartScanning();
            try {
                return new Watcher(_staticData, false);
            } finally {
                lock (ThreadSync) {
                    Monitor.Pulse(ThreadSync);
                }
            }
        }

        [CanBeNull]
        public static IList<DeviceInstance> Get() {
            return _staticData;
        }

        [NotNull, ItemCanBeNull]
        public static Task<IList<DeviceInstance>> GetAsync(CancellationToken cancellation) {
            if (_staticData != null) {
                return Task.FromResult(_staticData);
            }

            if (cancellation.IsCancellationRequested) {
                return Task.FromResult<IList<DeviceInstance>>(null);
            }

            StartScanning();
            return new Watcher(_staticData, true).GetAsync(cancellation);
        }

        [NotNull, ItemCanBeNull]
        public static Task<IList<DeviceInstance>> GetAsync() {
            return GetAsync(CancellationToken.None);
        }

        public class Watcher : NotifyPropertyChanged, IDisposable {
            public TimeSpan ScanTime => _scanTime;

            private IList<DeviceInstance> _instanceData;
            private readonly bool _oneTime;

            internal void RaiseScanTimeUpdate() {
                ActionExtension.InvokeInMainThreadAsync(() => {
                    OnPropertyChanged(nameof(ScanTime));
                });
            }

            internal void RaiseUpdate(IList<DeviceInstance> newData) {
                _instanceData = newData;

                for (var i = _waitingFor.Count - 1; i >= 0; i--) {
                    _waitingFor[i].TrySetResult(newData);
                }
                _waitingFor.Clear();

                if (_oneTime) {
                    Dispose();
                }

                ActionExtension.InvokeInMainThreadAsync(() => {
                    HasData = _instanceData != null;
                    Update?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(ScanTime));
                });
            }

            public Watcher(IList<DeviceInstance> staticData, bool oneTime) {
                _instanceData = staticData;
                _hasData = _instanceData != null;
                _oneTime = oneTime;

                lock (Instances) {
                    Instances.Add(this);
                }
            }

            [UsedImplicitly]
            public event EventHandler Update;

            private bool _hasData;

            public bool HasData {
                get => _hasData;
                private set => Apply(value, ref _hasData);
            }

            private List<TaskCompletionSource<IList<DeviceInstance>>> _waitingFor = new List<TaskCompletionSource<IList<DeviceInstance>>>();

            [CanBeNull]
            public IList<DeviceInstance> Get() {
                return _instanceData;
            }

            [NotNull, ItemCanBeNull]
            public Task<IList<DeviceInstance>> GetAsync() {
                return GetAsync(CancellationToken.None);
            }

            [NotNull, ItemCanBeNull]
            public Task<IList<DeviceInstance>> GetAsync(CancellationToken cancellation) {
                if (_instanceData != null) {
                    return Task.FromResult(_instanceData);
                }

                if (cancellation.IsCancellationRequested) {
                    return Task.FromResult<IList<DeviceInstance>>(null);
                }

                var result = new TaskCompletionSource<IList<DeviceInstance>>();
                RegisterCancellation(result, cancellation);
                RegisterWaiting(result);
                return result.Task;
            }

            private void RegisterCancellation(TaskCompletionSource<IList<DeviceInstance>> tcs, CancellationToken cancellation) {
                cancellation.Register(() => {
                    tcs.TrySetCanceled();
                    _waitingFor.Remove(tcs);
                });
            }

            private void RegisterWaiting(TaskCompletionSource<IList<DeviceInstance>> tcs) {
                _waitingFor.Add(tcs);
                tcs.Task.ContinueWith(t => _waitingFor.Remove(tcs));
            }

            private bool _disposed;

            public void Dispose() {
                for (var i = _waitingFor.Count - 1; i >= 0; i--) {
                    _waitingFor[i].TrySetCanceled();
                }
                _waitingFor.Clear();

                if (_disposed) return;
                _disposed = true;
                lock (Instances) {
                    Instances.Remove(this);
                }
            }
        }
    }
}