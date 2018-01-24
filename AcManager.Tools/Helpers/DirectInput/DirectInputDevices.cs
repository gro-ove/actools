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

namespace AcManager.Tools.Helpers.DirectInput {
    public static class DirectInputDevices {
        public static TimeSpan OptionMinRescanPeriod = TimeSpan.FromSeconds(1d);

        private static bool _threadStarted, _isActive;
        private static readonly object ThreadSync = new object();
        private static IList<DeviceInstance> _staticData;
        private static string _staticDataFootprint;

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

        private static void UpdateLists(IList<DeviceInstance> newData) {
            var footprint = newData?.Select(x => x.ProductGuid).JoinToString();
            if (footprint == _staticDataFootprint) return;

            _staticDataFootprint = footprint;
            _staticData = newData;
            lock (Instances) {
                foreach (var instance in Instances) {
                    instance.RaiseUpdate(newData);
                }
            }
        }

        private static void Scan() {
            var input = new SlimDX.DirectInput.DirectInput();

            try {
                var lastReport = Stopwatch.StartNew();
                while (_isActive) {
                    var getDevices = Stopwatch.StartNew();

                    IList<DeviceInstance> list;
                    try {
                        list = input?.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
                    } catch (Exception e) {
                        // TODO: Try to re-initiate scanning later?
                        Logging.Error(e);
                        list = new List<DeviceInstance>(0);
                        DisposeHelper.Dispose(ref input);
                    }

                    if (lastReport.Elapsed.Minutes > 1) {
                        Logging.Debug($"Time taken: {getDevices.Elapsed.TotalMilliseconds:F1} ms");
                        lastReport.Restart();
                    }

                    UpdateLists(list);
                    Task.Delay(OptionMinRescanPeriod + getDevices.Elapsed);

                    int count;
                    lock (Instances) {
                        count = Instances.Count;
                    }

                    if (count == 0) {
                        Monitor.Wait(ThreadSync);
                    }
                }
            } finally {
                DisposeHelper.Dispose(ref input);
            }
        }

        private static readonly List<Watcher> Instances = new List<Watcher>();

        [NotNull]
        public static Watcher Watch() {
            StartScanning();
            try {
                return new Watcher(_staticData, false);
            } finally {
                Monitor.Pulse(ThreadSync);
            }
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

        public class Watcher : NotifyPropertyChanged, IDisposable {
            private IList<DeviceInstance> _instanceData;
            private readonly bool _oneTime;

            internal void RaiseUpdate(IList<DeviceInstance> newData) {
                _instanceData = newData;

                _waitingFor.ForEach(x => x.TrySetResult(newData));
                _waitingFor.Clear();

                if (_oneTime) {
                    Dispose();
                }

                ActionExtension.InvokeInMainThreadAsync(() => {
                    HasData = _instanceData != null;
                    Update?.Invoke(this, EventArgs.Empty);
                });
            }

            public Watcher(IList<DeviceInstance> staticData, bool oneTime) {
                _instanceData = staticData;
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
                private set {
                    if (Equals(value, _hasData)) return;
                    _hasData = value;
                    OnPropertyChanged();
                }
            }

            private List<TaskCompletionSource<IList<DeviceInstance>>> _waitingFor = new List<TaskCompletionSource<IList<DeviceInstance>>>();

            [NotNull, ItemCanBeNull]
            // ReSharper disable once MemberHidesStaticFromOuterClass
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

            public void Dispose() {
                _waitingFor.ForEach(x => x.TrySetCanceled());
                _waitingFor.Clear();
                lock (Instances) {
                    Instances.Remove(this);
                }
            }
        }
    }
}