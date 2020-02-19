using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using AcManager.Tools.AcPlugins.Helpers;
using AcManager.Tools.AcPlugins.Messages;

namespace AcManager.Tools.AcPlugins.Info {
    [DataContract]
    public class DriverInfo {
        #region MsgCarUpdate cache  -  No idea how we use this, and if it's cool at all
        /// <summary>
        /// Defines how many MsgCarUpdates are cached (for a look in the past)
        /// </summary>
        [IgnoreDataMember]
        public static int MsgCarUpdateCacheSize { get; set; } = 0;

        [IgnoreDataMember]
        private LinkedList<MsgCarUpdate> _carUpdateCache = new LinkedList<MsgCarUpdate>();

        public LinkedListNode<MsgCarUpdate> LastCarUpdate => _carUpdateCache.Last;
        #endregion

        private const double MaxSpeed = 1000; // km/h
        private const double MinSpeed = 5; // km/h

        [DataMember]
        public int ConnectionId { get; set; }

        [DataMember]
        public long ConnectedTimestamp { get; set; } = -1;

        [DataMember]
        public long DisconnectedTimestamp { get; set; } = -1;

        [DataMember]
        public string DriverGuid { get; set; }

        [DataMember]
        public string DriverName { get; set; }

        [DataMember]
        public string DriverTeam { get; set; } // currently not set

        [DataMember]
        public byte CarId { get; set; }

        [DataMember]
        public string CarModel { get; set; }

        [DataMember]
        public string CarSkin { get; set; }

        [DataMember]
        public ushort BallastKg { get; set; } // currently not set

        [DataMember]
        public uint BestLap { get; set; }

        [DataMember]
        public uint TotalTime { get; set; }

        [DataMember]
        public ushort LapCount { get; set; }

        [DataMember]
        public ushort StartPosition { get; set; } // only set for race session

        [DataMember]
        public ushort Position { get; set; } // rename to e.g. Grid- or RacePosition? Easily mixed up with the Vector3 Positions

        [DataMember]
        public string Gap { get; set; }

        [DataMember]
        public int Incidents { get; set; }

        [DataMember]
        public float Distance { get; set; }

        [IgnoreDataMember]
        public float CurrentSpeed { get; set; } // km/h

        [IgnoreDataMember]
        public float CurrentAcceleration { get; set; } // km/h

        [IgnoreDataMember]
        public DateTime CurrentLapStart { get; set; } = DateTime.Now;

        [DataMember]
        public float TopSpeed { get; set; } // km/h

        [DataMember]
        public float StartSplinePosition { get; set; } = -1f;

        [DataMember]
        public float EndSplinePosition { get; set; } = -1f;

        [DataMember]
        public bool IsAdmin { get; set; }

        /// <summary>
        /// IMPORTANT: Is not automatically set! The plugin is responsible to determine this as we have no official
        /// way to do so by now. Just a field your plugin CAN use if you gather the information.
        /// </summary>
        [DataMember]
        public bool IsOnOutlap { get; set; }

        public bool IsConnected => ConnectedTimestamp != -1 && DisconnectedTimestamp == -1;

        public string BestLapText => AcServerPluginManager.FormatTimespan((int)BestLap);

        private int _lastTime = -1;
        private Vector3F _lastPosition;
        private Vector3F _lastVelocity;
        private float _lastSplinePos;

        private float _lapDistance;
        private float _lastDistanceTraveled;
        private float _lapStartSplinePos = -1f;

        #region getter for some 'realtime' positional info
        public float LapDistance => _lapDistance;

        [IgnoreDataMember]
        public float LastDistanceTraveled => _lastDistanceTraveled;

        public float LapStartSplinePos => _lapStartSplinePos;

        /// <summary>
        /// <see cref="Environment.TickCount"/> of the last position update.
        /// </summary>
        public int LastPositionUpdate => _lastTime;

        public Vector3F LastPosition => _lastPosition;

        public Vector3F LastVelocity => _lastVelocity;

        public float LastSplinePosition => _lastSplinePos;

        /// <summary>
        /// Expresses the distance in meters to the nearest car, either in front or back, ignoring positions.
        /// Zero if there is no other (moving) car
        /// </summary>
        [IgnoreDataMember]
        public float CurrentDistanceToClosestCar { get; set; }
        #endregion

        // That cache<MsgCarUpdate> should be replaced by a cache<CarUpdateThing> that also stores
        // the timestamp, otherwise calculations are always squishy (and e.g. dependent on the interval)
        public void UpdatePosition(MsgCarUpdate msg, TimeSpan realtimeUpdateInterval) {
            UpdatePosition(msg.WorldPosition, msg.Velocity, msg.NormalizedSplinePosition, realtimeUpdateInterval);
            if (MsgCarUpdateCacheSize > 0) {
                // We have to protect this cache from higher realtimeUpdateIntervals as requested
                if (_carUpdateCache.Count == 0
                        || (msg.CreationDate - _carUpdateCache.Last.Value.CreationDate).TotalMilliseconds >= realtimeUpdateInterval.TotalMilliseconds * 0.9991) {
                    var node = _carUpdateCache.AddLast(msg);
                    if (_carUpdateCache.Count > MsgCarUpdateCacheSize) {
                        _carUpdateCache.RemoveFirst();
                    }

                    if (_carUpdateCache.Count > 1) {
                        // We could easily do car-specifc stuff here, e.g. calculate the distance driven between the intervals,
                        // or a python-app like delta - maybe even a loss of control
                    }
                }
            }
        }

        public void UpdatePosition(Vector3F pos, Vector3F vel, float s, TimeSpan realtimeUpdateInterval) {
            if (StartSplinePosition == -1.0f) {
                StartSplinePosition = s > 0.5f ? s - 1.0f : s;
            }

            if (_lapStartSplinePos == -1.0f) {
                _lapStartSplinePos = s > 0.5f ? s - 1.0f : s;
            }

            // Determine the current speed in KpH (only valid if the update interval is 1s)
            CurrentSpeed = vel.Length() * 3.6f;
            if (CurrentSpeed < MaxSpeed && CurrentSpeed > TopSpeed) {
                TopSpeed = CurrentSpeed;
            }

            // Determine the current acceleration in Kph/s (only valid if the update interval is 1s)
            var lastSpeed = _lastVelocity.Length() * 3.6f;
            CurrentAcceleration = (CurrentSpeed - lastSpeed) / (float)realtimeUpdateInterval.TotalSeconds;

            // See https://msdn.microsoft.com/de-de/library/system.environment.tickcount%28v=vs.110%29.aspx
            var currentTime = Environment.TickCount & Int32.MaxValue;
            var elapsedSinceLastUpdate = currentTime - _lastTime;
            if (_lastTime > 0 && elapsedSinceLastUpdate > 0 && elapsedSinceLastUpdate < 3 * realtimeUpdateInterval.TotalSeconds) {
                var d = (pos - _lastPosition).Length();
                var speed = d / elapsedSinceLastUpdate / 1000 * 3.6f;

                // If the computed average speed since last update is not much bigger than the maximum of last vel and the current vel then no warp detected.
                // in worst case warps that occur from near the pits (~50m) are not detected.
                if (speed - Math.Max(CurrentSpeed, lastSpeed) < 180d * elapsedSinceLastUpdate / 1000) {
                    // no warp detected
                    _lapDistance += d;
                    Distance += d;
                    _lastDistanceTraveled = d;

                    if (CurrentSpeed > MinSpeed) {
                        // don't update LastSplinePos if car is moving very slowly (was send to box?)
                        EndSplinePosition = s;
                    }
                } else {
                    // Probably warped to box
                    _lapDistance = 0;
                    _lapStartSplinePos = s > 0.5f ? s - 1.0f : s;
                    CurrentLapStart = DateTime.Now;
                }
            }
            _lastPosition = pos;
            _lastVelocity = vel;
            _lastSplinePos = s;
            _lastTime = currentTime;
        }

        public float OnLapCompleted() {
            var lastSplinePos = EndSplinePosition;
            if (lastSplinePos < 0.5) {
                lastSplinePos += 1f;
            }

            var splinePosDiff = lastSplinePos - _lapStartSplinePos;
            var lapLength = _lapDistance / splinePosDiff;

            _lapStartSplinePos = lastSplinePos - 1f;
            _lapDistance = 0f;
            _lastSplinePos = 0.0f;
            CurrentLapStart = DateTime.Now;

            return lapLength;
        }
    }
}