using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Timers;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

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

        private Timer _timer;

        private AcSharedMemory() {
            SettingsHolder.Drive.PropertyChanged += OnDrivePropertyChanged;

            _timer = new Timer { AutoReset = true };
            _timer.Elapsed += Update;

            Status = SettingsHolder.Drive.WatchForSharedMemory ? AcSharedMemoryStatus.Disconnected :
                    AcSharedMemoryStatus.Disabled;
        }

        private void OnDrivePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.Drive.WatchForSharedMemory)) {
                Status = SettingsHolder.Drive.WatchForSharedMemory ? AcSharedMemoryStatus.Disconnected :
                        AcSharedMemoryStatus.Disabled;
            }
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
                        _timer.Enabled = false;
                        ResetFpsCounter(false);
                        break;
                    case AcSharedMemoryStatus.Connected:
                        _timer.Interval = OptionNotLiveReadingInterval;
                        _timer.Enabled = true;
                        ResetFpsCounter(false);
                        break;
                    case AcSharedMemoryStatus.Connecting:
                        _timer.Enabled = false;
                        ResetFpsCounter(false);
                        break;
                    case AcSharedMemoryStatus.Live:
                        _timer.Interval = OptionLiveReadingInterval;
                        _timer.Enabled = true;
                        Start?.Invoke(this, EventArgs.Empty);
                        break;
                    case AcSharedMemoryStatus.Disconnected:
                        _timer.Interval = OptionDisconnectedReadingInterval;
                        _timer.Enabled = true;
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
        private MemoryMappedFile _physicsFile;
        private MemoryMappedFile _graphicsFile;
        private MemoryMappedFile _staticInfoFile;

        private Process _gameProcess;

        [CanBeNull]
        public Process GameProcess => _gameProcess;

        private Stopwatch _samePackedIdTime;

        private int _graphicsPacketId, _graphicsFrames, _fpsSamples;
        private Stopwatch _graphicsTime;
        private double _averageFps;
        private double? _minimumFps;

        public class FpsDetails {
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
            if (_physicsFile == null) return;

            try {
                AcSharedGraphics graphics;

                if (MonitorFramesPerSecond) {
                    graphics = _graphicsFile.ToStruct<AcSharedGraphics>(AcSharedGraphics.Buffer);

                    var delta = graphics.PacketId - _graphicsPacketId;
                    _graphicsPacketId = graphics.PacketId;

                    if (_graphicsTime == null) {
                        _graphicsTime = new Stopwatch();
                    }

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
                } else {
                    graphics = null;
                }

                var physics = _physicsFile.ToStruct<AcSharedPhysics>(AcSharedPhysics.Buffer);
                if (physics.PacketId != _previousPacketId) {
                    var firstPacket = !_previousPacketId.HasValue;
                    _previousPacketId = physics.PacketId;
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

                    if (graphics == null) {
                        graphics = _graphicsFile.ToStruct<AcSharedGraphics>(AcSharedGraphics.Buffer);
                    }

                    var staticInfo = _staticInfoFile.ToStruct<AcSharedStaticInfo>(AcSharedStaticInfo.Buffer);
                    Shared = new AcShared(physics, graphics, staticInfo);
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
                DisposeHelper.Dispose(ref _physicsFile);
                DisposeHelper.Dispose(ref _graphicsFile);
                DisposeHelper.Dispose(ref _staticInfoFile);

                _physicsFile = MemoryMappedFile.OpenExisting(@"Local\acpmf_physics");
                _graphicsFile = MemoryMappedFile.OpenExisting(@"Local\acpmf_graphics");
                _staticInfoFile = MemoryMappedFile.OpenExisting(@"Local\acpmf_static");

                Status = AcSharedMemoryStatus.Connected;
                return true;
            } catch (FileNotFoundException) {
                Status = AcSharedMemoryStatus.Disconnected;
                return false;
            }
        }

        private void Update(object sender, ElapsedEventArgs e) {
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
            DisposeHelper.Dispose(ref _timer);
            DisposeHelper.Dispose(ref _gameProcess);
            DisposeHelper.Dispose(ref _physicsFile);
            DisposeHelper.Dispose(ref _graphicsFile);
            DisposeHelper.Dispose(ref _staticInfoFile);
        }
    }
}