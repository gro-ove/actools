using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Timers;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

namespace AcManager.Tools.SharedMemory {
    public class AcSharedMemory : NotifyPropertyChanged, IDisposable {
        public static double OptionLiveReadingInterval = 50d;
        public static double OptionNotLiveReadingInterval = 200d;
        public static double OptionDisconnectedReadingInterval = 2000d;

        public static AcSharedMemory Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null, @"Already initialized");
            Instance = new AcSharedMemory();
        }

        public event EventHandler Tick;
        public event EventHandler Connect;
        private Timer _runTimer;

        private AcSharedMemory() {
            SettingsHolder.Drive.PropertyChanged += OnDrivePropertyChanged;

            _runTimer = new Timer { AutoReset = true };
            _runTimer.Elapsed += Update;
            Status = SettingsHolder.Drive.WatchForSharedMemory ? AcSharedMemoryStatus.Disconnected :
                    AcSharedMemoryStatus.Disabled;
        }

        private void OnDrivePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.Drive.WatchForSharedMemory)) {
                Status = SettingsHolder.Drive.WatchForSharedMemory ? AcSharedMemoryStatus.Disconnected :
                        AcSharedMemoryStatus.Disabled;
            }
        }

        private void UpdateCollectionParams(bool active, double intervalMs = 0d) {
            if (intervalMs != 0d) {
                _runTimer.Interval = intervalMs;
            }
            _runTimer.Enabled = active;
        }

        private AcSharedMemoryStatus? _statusValue;

        internal AcSharedMemoryStatus Status {
            get => _statusValue ?? AcSharedMemoryStatus.Disconnected;
            private set {
                if (Equals(value, _statusValue)) return;

                // Logging.Debug("SM status changed: " + value);

                if (_statusValue == AcSharedMemoryStatus.Live) {
                    Finish?.Invoke(this, EventArgs.Empty);
                }

                if (_statusValue == AcSharedMemoryStatus.Connected) {
                    DisposeHelper.Dispose(ref _gameProcess);
                }

                _statusValue = value;
                OnPropertyChanged();

                switch (_statusValue) {
                    case AcSharedMemoryStatus.Disabled:
                        UpdateCollectionParams(false);
                        ResetFpsCounter(false);
                        break;
                    case AcSharedMemoryStatus.Connected:
                        UpdateCollectionParams(true, OptionNotLiveReadingInterval);
                        ResetFpsCounter(false);
                        break;
                    case AcSharedMemoryStatus.Connecting:
                        UpdateCollectionParams(false);
                        ResetFpsCounter(false);
                        break;
                    case AcSharedMemoryStatus.Live:
                        UpdateCollectionParams(true, OptionLiveReadingInterval);
                        Start?.Invoke(this, EventArgs.Empty);
                        break;
                    case AcSharedMemoryStatus.Disconnected:
                        UpdateCollectionParams(true, OptionDisconnectedReadingInterval);
                        _previousPacketId = null;
                        ResetFpsCounter(false);
                        break;
                }
            }
        }

        public bool IsLive => Status == AcSharedMemoryStatus.Live;

        private bool _isPaused;

        public bool IsPaused {
            get => _isPaused;
            set {
                if (Equals(value, _isPaused)) return;
                _isPaused = value;
                OnPropertyChanged();
                PauseTime = DateTime.Now;
                (value ? Pause : Resume)?.Invoke(this, EventArgs.Empty);
            }
        }

        private DateTime _pauseTime;

        public DateTime PauseTime {
            get => _pauseTime;
            private set => Apply(value, ref _pauseTime);
        }

        private bool _monitorFramesPerSecond;

        public bool MonitorFramesPerSecond {
            get => _monitorFramesPerSecond;
            set => Apply(value, ref _monitorFramesPerSecond, () => ResetFpsCounter(value));
        }

        private AcShared _shared;
        private DateTime _sharedTime;

        [CanBeNull]
        public AcShared Shared {
            get => _shared;
            set {
                if (Equals(value, _shared)) return;
                var prev = _shared;
                var prevTime = _sharedTime;
                _shared = value;
                _sharedTime = DateTime.Now;
                OnPropertyChanged();
                Updated?.Invoke(this, new AcSharedEventArgs(prev, _shared, prev == null ? TimeSpan.Zero : _sharedTime - prevTime));
            }
        }

        public bool KnownProcess { get; private set; }

        private int? _previousPacketId;
        private DateTime _previousPacketTime;
        private BetterMemoryMappedAccessor<AcSharedPhysics> _physicsMmFile;
        private BetterMemoryMappedAccessor<AcSharedGraphics> _graphicsMmFile;
        private BetterMemoryMappedAccessor<AcSharedStaticInfo> _staticInfoMmFile;

        private Process _gameProcess;

        [CanBeNull]
        public Process GameProcess => _gameProcess;

        private Stopwatch _samePackedIdTime;

        private int _graphicsPacketId, _graphicsFrames, _fpsSamples;
        private Stopwatch _graphicsTime;
        private double _averageFps;
        private double? _minimumFps;

        public class FpsDetails : NotifyPropertyChanged {
            [JsonConstructor]
            public FpsDetails(double averageFps, double? minimumFps, int samplesTaken) {
                AverageFps = averageFps;
                MinimumFps = minimumFps;
                SamplesTaken = samplesTaken;
            }

            [JsonProperty("averageFps")]
            public double AverageFps { get; }

            [JsonProperty("minimumFps")]
            public double? MinimumFps { get; }

            [JsonProperty("samplesTaken")]
            public int SamplesTaken { get; }
        }

        private FpsDetails _lastFpsDetails;

        [CanBeNull]
        public FpsDetails GetFpsDetails() {
            return _fpsSamples > 0 ? new FpsDetails(_averageFps, _minimumFps, _fpsSamples) : _lastFpsDetails;
        }

        private void ResetFpsCounter(bool resetLastDetails) {
            if (_fpsSamples > 0) {
                MonitorFramesPerSecondEnd?.Invoke(this, EventArgs.Empty);
            }

            if (resetLastDetails) {
                _lastFpsDetails = null;
            } else if (_fpsSamples > 0) {
                _lastFpsDetails = new FpsDetails(_averageFps, _minimumFps, _fpsSamples);
            }

            _graphicsTime = null;
            _graphicsFrames = 0;
            _fpsSamples = 0;
            _averageFps = 0;
            _minimumFps = null;
        }

        public static readonly TimeSpan MonitorFramesPerSecondSampleFrequency = TimeSpan.FromSeconds(2d);

        public event EventHandler MonitorFramesPerSecondBegin;
        public event EventHandler MonitorFramesPerSecondEnd;

        private void UpdateData() {
            if (_physicsMmFile == null) return;

            try {
                // AcSharedGraphics graphics;

                if (MonitorFramesPerSecond) {
                    if (_graphicsTime == null) {
                        _graphicsTime = new Stopwatch();
                    }

                    var packetId = _graphicsMmFile.GetPacketId();
                    var delta = packetId - _graphicsPacketId;
                    _graphicsPacketId = packetId;

                    if (delta > 0) {
                        if (_graphicsTime.IsRunning) {
                            _graphicsFrames += delta;
                            var seconds = _graphicsTime.Elapsed.TotalSeconds;
                            if (seconds > MonitorFramesPerSecondSampleFrequency.TotalSeconds) {
                                var fps = _graphicsFrames / seconds / 2;

                                if (_fpsSamples == 0) {
                                    MonitorFramesPerSecondBegin?.Invoke(this, EventArgs.Empty);
                                }

                                _averageFps = (_averageFps * _fpsSamples + fps) / (_fpsSamples + 1);
                                _fpsSamples++;
                                if (_fpsSamples > 1 && (!_minimumFps.HasValue || _minimumFps > fps)) {
                                    _minimumFps = fps;
                                }

                                // Logging.Debug($"FPS: {fps:F1}");
                                _graphicsFrames = 0;
                                _graphicsTime.Restart();
                            }
                        }

                        _graphicsTime.Start();
                    } else {
                        _graphicsTime.Stop();
                    }
                }

                var physicsPacketId = _physicsMmFile.GetPacketId();
                if (physicsPacketId != _previousPacketId) {
                    var firstPacket = !_previousPacketId.HasValue;
                    _previousPacketId = physicsPacketId;
                    _previousPacketTime = DateTime.Now;

                    if (firstPacket) {
                        IsPaused = true;
                        return;
                    }

                    IsPaused = false;
                    if (Status != AcSharedMemoryStatus.Live) {
                        Status = AcSharedMemoryStatus.Live;
                        _gameProcess = AcProcess.TryToFind();
                        KnownProcess = _gameProcess != null;
                    }

                    // TODO: Optimize further to avoid excessive copying?
                    Shared = new AcShared(
                            _physicsMmFile.Get(), 
                            _graphicsMmFile.Get(),
                            _staticInfoMmFile);
                    Tick?.Invoke(this, EventArgs.Empty);
                } else if (_gameProcess?.HasExitedSafe() ?? (DateTime.Now - _previousPacketTime).TotalSeconds > 1d) {
                    IsPaused = false;
                    Status = AcSharedMemoryStatus.Connected;
                    Shared = null;
                } else {
                    var shared = Shared;
                    if (shared == null || shared.Graphics.Status == AcGameStatus.AcPause) {
                        IsPaused = true;
                    } else {
                        // Sometimes, AC doesn’t bother setting Status flag to AcPause
                        if (_samePackedIdTime == null) {
                            _samePackedIdTime = Stopwatch.StartNew();
                            IsPaused = false;
                        } else if (_samePackedIdTime.ElapsedMilliseconds > 50) {
                            // Might be a false positive at 20 FPS, but who would play like that anyway?
                            IsPaused = true;
                        } else {
                            IsPaused = false;
                        }
                    }
                }
            } catch (Exception ex) {
                Logging.Error(ex);
                Status = AcSharedMemoryStatus.Disabled;
            }
        }

        private bool Reconnect() {
            Status = AcSharedMemoryStatus.Connecting;

            try {
                DisposeHelper.Dispose(ref _physicsMmFile);
                DisposeHelper.Dispose(ref _graphicsMmFile);
                DisposeHelper.Dispose(ref _staticInfoMmFile);

                _physicsMmFile = new BetterMemoryMappedAccessor<AcSharedPhysics>(@"Local\acpmf_physics");
                _graphicsMmFile = new BetterMemoryMappedAccessor<AcSharedGraphics>(@"Local\acpmf_graphics");
                _staticInfoMmFile = new BetterMemoryMappedAccessor<AcSharedStaticInfo>(@"Local\acpmf_static");

                Status = AcSharedMemoryStatus.Connected;
                Connect?.Invoke(this, EventArgs.Empty);
                return true;
            } catch (FileNotFoundException) {
                Status = AcSharedMemoryStatus.Disconnected;
                return false;
            }
        }

        private void UpdateInner() {
            try {
                switch (Status) {
                    case AcSharedMemoryStatus.Disconnected:
                        if (Reconnect()) {
                            UpdateData();
                        }
                        break;

                    case AcSharedMemoryStatus.Connected:
                    case AcSharedMemoryStatus.Live:
                        UpdateData();
                        break;

                    case AcSharedMemoryStatus.Connecting:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            } catch (Exception ex) {
#if DEBUG
                Logging.Warning("ERROR: " + ex);
#endif
            }
        }

        private void Update(object sender, ElapsedEventArgs e) {
            UpdateInner();
        }

        /// <summary>
        /// Game started.
        /// </summary>
        public event EventHandler Start;

        /// <summary>
        /// Game paused.
        /// </summary>
        public event EventHandler Pause;

        /// <summary>
        /// Game resumed.
        /// </summary>
        public event EventHandler Resume;

        /// <summary>
        /// Game finished.
        /// </summary>
        public event EventHandler Finish;

        /// <summary>
        /// New physics data arrived.
        /// </summary>
        public event EventHandler<AcSharedEventArgs> Updated;

        public void Dispose() {
            DisposeHelper.Dispose(ref _runTimer);
            DisposeHelper.Dispose(ref _gameProcess);
            DisposeHelper.Dispose(ref _physicsMmFile);
            DisposeHelper.Dispose(ref _graphicsMmFile);
            DisposeHelper.Dispose(ref _staticInfoMmFile);
        }
    }
}