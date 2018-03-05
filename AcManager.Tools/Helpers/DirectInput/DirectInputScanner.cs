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
        private static TimeSpan _scanTime;
        private static readonly object ThreadSync = new object();

        [CanBeNull]
        private static IList<Joystick> _staticData;

        [CanBeNull]
        private static string _staticDataFootprint;

        [CanBeNull]
        private static SlimDX.DirectInput.DirectInput _directInput;

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

                    IList<Joystick> list;
                    string footprint;
                    bool updated;

                    if (_directInput == null) {
                        list = new List<Joystick>(0);
                        footprint = string.Empty;
                        updated = _staticDataFootprint != footprint;
                    } else {
                        try {
                            var devices = _directInput?.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
                            footprint = devices?.Select(x => x.InstanceGuid).JoinToString(';');
                            updated = _staticDataFootprint != footprint;
                            list = updated ? devices?.Select(x => {
                                var existing = _staticData?.FirstOrDefault(y =>
                                        y.Information.InstanceGuid == x.InstanceGuid);
                                if (existing != null) {
                                    return existing;
                                }

                                var result = new Joystick(_directInput, x.InstanceGuid);
                                if (result.Capabilities == null) {
                                    // We don’t really need a check here, but we need to access .Capabilities here, in a background
                                    // thread, because it might take a while to get the data which will be needed later.
                                    throw new Exception("Never happens");
                                }

                                return result;
                            }).ToArray() : _staticData;
                        } catch (Exception e) {
                            // TODO: Try to re-initiate scanning later?
                            Logging.Error(e);
                            list = new List<Joystick>(0);
                            footprint = string.Empty;
                            updated = _staticDataFootprint != footprint;
                            DisposeHelper.Dispose(ref _directInput);
                        }
                    }

                    getDevices.Stop();
                    _scanTime = getDevices.Elapsed;

                    if (updated) {
                        _staticData?.ApartFrom(list).DisposeEverything();
                        _staticDataFootprint = footprint;
                        _staticData = list;
                        lock (Instances) {
                            for (var i = Instances.Count - 1; i >= 0; i--) {
                                Instances[i].RaiseUpdate(list);
                            }
                        }
                    } else {
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
        public static IList<Joystick> Get() {
            return _staticData;
        }

        [NotNull, ItemCanBeNull]
        public static Task<IList<Joystick>> GetAsync(CancellationToken cancellation) {
            if (_staticData != null) {
                return Task.FromResult(_staticData);
            }

            if (cancellation.IsCancellationRequested) {
                return Task.FromResult<IList<Joystick>>(null);
            }

            StartScanning();
            return new Watcher(_staticData, true).GetAsync(cancellation);
        }

        [NotNull, ItemCanBeNull]
        public static Task<IList<Joystick>> GetAsync() {
            return GetAsync(CancellationToken.None);
        }

        public class Watcher : NotifyPropertyChanged, IDisposable {
            public TimeSpan ScanTime => _scanTime;

            private IList<Joystick> _instanceData;
            private readonly bool _oneTime;

            internal void RaiseScanTimeUpdate() {
                ActionExtension.InvokeInMainThreadAsync(() => { OnPropertyChanged(nameof(ScanTime)); });
            }

            internal void RaiseUpdate(IList<Joystick> newData) {
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

            public Watcher(IList<Joystick> staticData, bool oneTime) {
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

            private readonly List<TaskCompletionSource<IList<Joystick>>> _waitingFor = new List<TaskCompletionSource<IList<Joystick>>>();

            [CanBeNull]
            public IList<Joystick> Get() {
                return _instanceData;
            }

            [NotNull, ItemCanBeNull]
            public Task<IList<Joystick>> GetAsync() {
                return GetAsync(CancellationToken.None);
            }

            [NotNull, ItemCanBeNull]
            public Task<IList<Joystick>> GetAsync(CancellationToken cancellation) {
                if (_instanceData != null) {
                    return Task.FromResult(_instanceData);
                }

                if (cancellation.IsCancellationRequested) {
                    return Task.FromResult<IList<Joystick>>(null);
                }

                var result = new TaskCompletionSource<IList<Joystick>>();
                RegisterCancellation(result, cancellation);
                RegisterWaiting(result);
                return result.Task;
            }

            private void RegisterCancellation(TaskCompletionSource<IList<Joystick>> tcs, CancellationToken cancellation) {
                cancellation.Register(() => {
                    tcs.TrySetCanceled();
                    _waitingFor.Remove(tcs);
                });
            }

            private void RegisterWaiting(TaskCompletionSource<IList<Joystick>> tcs) {
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