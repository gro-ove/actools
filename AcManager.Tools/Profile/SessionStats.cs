using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AcManager.Tools.Helpers;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using SlimDX;

namespace AcManager.Tools.Profile {
    public partial class PlayerStatsManager {
        /// <summary>
        /// For best laps registration.
        /// </summary>
        public static int OptionTyresOutsideAllowed = 2;

        /// <summary>
        /// For jumps, tricks, average speed and fuel consumation.
        /// </summary>
        public static float OptionMinSpeedKmh = 10f;

        /// <summary>
        /// Velocity by Y axis.
        /// </summary>
        public static float OptionFallingThreshold = -2.5f;

        public class SessionStatsEventArgs : EventArgs {
            public SessionStatsEventArgs(SessionStats stats) {
                Stats = stats;
            }

            public SessionStats Stats { get; }
        }

        public class SessionStats : NotifyPropertyChanged {
            [JsonProperty]
            public DateTime StartedAt { get; internal set; }

            [JsonProperty]
            public string CarId { get; internal set; }

            [JsonProperty]
            public string TrackId { get; internal set; }

            [JsonProperty]
            public double MaxSpeed { get; internal set; }

            [JsonProperty]
            public double Distance { get; internal set; }

            [JsonProperty]
            public TimeSpan Time { get; internal set; }

            [JsonProperty]
            public double FuelBurnt { get; internal set; }

            [JsonProperty]
            public int GoneOffroad { get; internal set; }

            [JsonProperty]
            public double LongestAirborne { get; internal set; }

            [JsonProperty]
            public double TotalAirborne { get; internal set; }

            [JsonProperty]
            public double LongestWheelie { get; internal set; }

            [JsonProperty]
            public double TotalWheelie { get; internal set; }

            [JsonProperty]
            public double LongestTwoWheels { get; internal set; }

            [JsonProperty]
            public double TotalTwoWheels { get; internal set; }

            [JsonProperty]
            public double TotalTyreWear { get; internal set; }

            [JsonProperty]
            public int TotalCrashes { get; internal set; }

            [JsonProperty]
            public bool Penalties { get; internal set; }

            [JsonProperty]
            public TimeSpan? BestLap { get; internal set; }

            [JsonProperty]
            public int? BestLapId { get; internal set; }

            #region Calculated on-fly
            [JsonIgnore]
            public double DistanceKm => Distance / 1e3;

            [JsonIgnore]
            public double AverageSpeed => Equals(Time.TotalHours, 0d) ? 0d : DistanceKm / Time.TotalHours;

            [JsonIgnore]
            public double FuelConsumption => Equals(DistanceKm, 0d) ? 0d : 1e2 * FuelBurnt / DistanceKm;
            #endregion

            public string Serialize() {
                var s = new StringWriter();
                var w = new JsonTextWriter(s);
                w.WriteStartObject();

                w.Write(nameof(CarId), CarId);
                w.Write(nameof(TrackId), TrackId);

                w.Write(nameof(StartedAt), StartedAt);
                w.Write(nameof(Time), Time);
                w.Write(nameof(BestLap), BestLap);
                w.Write(nameof(BestLapId), BestLapId);

                w.WriteNonDefault(nameof(GoneOffroad), GoneOffroad);
                w.WriteNonDefault(nameof(Penalties), Penalties);

                w.WriteNonDefault(nameof(MaxSpeed), MaxSpeed, "F1");
                w.WriteNonDefault(nameof(Distance), Distance, "F1");
                w.WriteNonDefault(nameof(FuelBurnt), FuelBurnt, "F1");
                w.WriteNonDefault(nameof(LongestAirborne), LongestAirborne, "F3");
                w.WriteNonDefault(nameof(LongestWheelie), LongestWheelie, "F3");
                w.WriteNonDefault(nameof(LongestTwoWheels), LongestTwoWheels, "F3");
                w.WriteNonDefault(nameof(TotalAirborne), TotalAirborne, "F2");
                w.WriteNonDefault(nameof(TotalWheelie), TotalWheelie, "F2");
                w.WriteNonDefault(nameof(TotalTwoWheels), TotalTwoWheels, "F2");
                w.WriteNonDefault(nameof(TotalTyreWear), TotalTyreWear, "F3");

                w.WriteEndObject();
                return s.ToString();
            }

            public static SessionStats Deserialize(string data) {
                try {
                    using (var textReader = new StringReader(data)) {
                        var reader = new JsonTextReader(textReader);

                        var r = new SessionStats();
                        var currentProperty = string.Empty;

                        reader.MatchNext(JsonToken.StartObject);
                        while (reader.Until(JsonToken.EndObject)) {
                            switch (reader.TokenType) {
                                case JsonToken.PropertyName:
                                    currentProperty = reader.Value.ToString();
                                    break;

                                case JsonToken.String:
                                    switch (currentProperty) {
                                        case nameof(CarId):
                                            r.CarId = reader.Value.ToString();
                                            break;
                                        case nameof(TrackId):
                                            r.TrackId = reader.Value.ToString();
                                            break;
                                        case nameof(Time):
                                            r.Time = TimeSpan.Parse(reader.Value.ToString());
                                            break;
                                        case nameof(BestLap):
                                            r.BestLap = TimeSpan.Parse(reader.Value.ToString());
                                            break;
                                        default:
                                            throw new Exception($"Unknown key: {currentProperty} (String)");
                                    }
                                    break;

                                case JsonToken.Date:
                                    switch (currentProperty) {
                                        case nameof(StartedAt):
                                            r.StartedAt = DateTime.Parse(reader.Value.ToString());
                                            break;
                                        default:
                                            throw new Exception($"Unknown key: {currentProperty} (Date)");
                                    }
                                    break;

                                case JsonToken.Float:
                                case JsonToken.Integer:
                                    var val = double.Parse(reader.Value.ToString(), CultureInfo.InvariantCulture);
                                    switch (currentProperty) {
                                        case nameof(MaxSpeed):
                                            r.MaxSpeed = val;
                                            break;
                                        case nameof(Distance):
                                            r.Distance = val;
                                            break;
                                        case nameof(FuelBurnt):
                                            r.FuelBurnt = val;
                                            break;
                                        case nameof(LongestAirborne):
                                            r.LongestAirborne = val;
                                            break;
                                        case nameof(TotalAirborne):
                                            r.TotalAirborne = val;
                                            break;
                                        case nameof(LongestWheelie):
                                            r.LongestWheelie = val;
                                            break;
                                        case nameof(TotalWheelie):
                                            r.TotalWheelie = val;
                                            break;
                                        case nameof(LongestTwoWheels):
                                            r.LongestTwoWheels = val;
                                            break;
                                        case nameof(TotalTwoWheels):
                                            r.TotalTwoWheels = val;
                                            break;
                                        case nameof(TotalTyreWear):
                                            r.TotalTyreWear = val;
                                            break;
                                        case nameof(GoneOffroad):
                                            r.GoneOffroad = (int)val;
                                            break;
                                        case nameof(TotalCrashes):
                                            r.TotalCrashes = (int)val;
                                            break;
                                        case nameof(BestLapId):
                                            r.BestLapId = (int?)val;
                                            break;
                                        default:
                                            throw new Exception($"Unknown key: {currentProperty} (Integer/Float)");
                                    }
                                    break;

                                case JsonToken.Boolean:
                                    switch (currentProperty) {
                                        case nameof(Penalties):
                                            r.Penalties = bool.Parse(reader.Value.ToString());
                                            break;
                                        default:
                                            throw new Exception($"Unknown key: {currentProperty} (Boolean)");
                                    }
                                    break;

                                default:
                                    throw new Exception($"Unexpected token: {reader.TokenType}");
                            }
                        }

                        return r;
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    return JsonConvert.DeserializeObject<SessionStats>(data);
                }
            }
        }

        /// <summary>
        /// Doesn’t spawn PropertyChanged event — don’t bind while game is still running.
        /// </summary>
        public class WatchingSessionStats : SessionStats {
            internal WatchingSessionStats() {
                StartedAt = DateTime.Now;
            }

            public enum Status {
                NotLive,
                Falling,
                Paused,
                Teleported,
                Airborne,
                Wheelie,
                TwoWheels,
                Live
            }

            private Status _currentStatus;

            // [JsonProperty, JsonConverter(typeof(StringEnumConverter))]
            [JsonIgnore]
            public Status CurrentStatus {
                get { return _currentStatus; }
                internal set {
                    if (Equals(_currentStatus, value)) return;

                    switch (value) {
                        case Status.Teleported:
                            _completedLaps = 0;
                            break;

                        case Status.Live:
                            switch (_currentStatus) {
                                case Status.Airborne:
                                    if (_statusStopwatch.Elapsed.TotalSeconds > 0.5) {
                                        var distance = new Vector3(_coordinates.X - _statusStart.X, 0f, _coordinates.Z - _statusStart.Z).Length();
                                        LongestAirborne = Math.Max(LongestAirborne, distance);
                                        TotalAirborne += distance;
                                    }
                                    break;

                                case Status.Wheelie:
                                    if (_statusStopwatch.Elapsed.TotalSeconds > 1d) {
                                        var distance = (_coordinates - _statusStart).Length();
                                        LongestWheelie = Math.Max(LongestAirborne, distance);
                                        TotalWheelie += distance;
                                    }
                                    break;

                                case Status.TwoWheels:
                                    if (_statusStopwatch.Elapsed.TotalSeconds > 1d) {
                                        var distance = (_coordinates - _statusStart).Length();
                                        LongestTwoWheels = Math.Max(LongestAirborne, distance);
                                        TotalTwoWheels += distance;
                                    }
                                    break;
                            }
                            break;
                    }

                    _currentStatus = value;
                    _statusStart = _coordinates;
                    _statusStopwatch = Stopwatch.StartNew();
                }
            }

            #region Internal status variables
            private bool _offroad;
            private int _lapSpoiledId, _completedLaps;
            private DateTime _offroadTime, _crashTime;
            private Vector3 _coordinates;
            private Vector3 _statusStart;
            private Stopwatch _statusStopwatch;
            #endregion

            public void Extend(AcShared previous, AcShared current, TimeSpan time) {
                if (current == null) {
                    _offroad = false;
                    return;
                }

                if (previous == null || current.Graphics.Status != AcGameStatus.AcLive) {
                    CurrentStatus = Status.NotLive;
                    return;
                }

                var physics = current.Physics;
                var graphics = current.Graphics;
                var info = current.StaticInfo;

                if (CarId == null) {
                    CarId = info.CarModel;
                    TrackId = string.IsNullOrWhiteSpace(info.TrackConfiguration) ? info.Track :
                            $"{info.Track}/{info.TrackConfiguration}";
                    Penalties = info.PenaltiesEnabled == 1;
                }

                _coordinates = graphics.CarCoordinates;

                if (graphics.CurrentTimeMs < previous.Graphics.CurrentTimeMs) {
                    CurrentStatus = Status.Teleported;
                    return;
                }

                var distance = graphics.CarCoordinates - previous.Graphics.CarCoordinates;
                var calcSpeed = distance.Length() / 1e3 / time.TotalHours;

                if (graphics.IsInPit || physics.NumberOfTyresOut > OptionTyresOutsideAllowed) {
                    _lapSpoiledId = graphics.CompletedLaps;
                }

                /* best lap */
                if (graphics.CompletedLaps != _completedLaps) {
                    if (graphics.LastTimeMs == 0) {
                        Logging.Debug("Lap time: 0");
                    } else if (graphics.CompletedLaps - 1 > _lapSpoiledId) {
                        var lastTime = TimeSpan.FromMilliseconds(graphics.LastTimeMs);
                        if (!BestLap.HasValue || lastTime <= BestLap) {
                            BestLap = lastTime;
                            BestLapId = graphics.CompletedLaps - 1;
                            Logging.Debug("New lap time: " + lastTime.ToProperString());
                        } else {
                            Logging.Debug("Lap time is worse or the same");
                        }
                    } else {
                        Logging.Debug("Lap spoiled");
                    }

                    _completedLaps = graphics.CompletedLaps;
                }

                if (distance.Y < OptionFallingThreshold) {
                    CurrentStatus = Status.Falling;
                    return;
                }

                var paused = physics.SpeedKmh > 5 && calcSpeed < physics.SpeedKmh * 0.1d;
                if (paused) {
                    CurrentStatus = Status.Paused;
                    return;
                }

                var jumped = calcSpeed > physics.SpeedKmh * 3d + 10;
                if (jumped) {
                    CurrentStatus = Status.Teleported;
                    return;
                }

                for (var i = 0; i < 4; i++) {
                    var d = previous.Physics.TyreWear[i] - physics.TyreWear[i];
                    if (d > 0 && d < 0.1) {
                        /* TyreWear’ range is from 0.0 to 100.0 */
                        TotalTyreWear += d / 100d;
                    }
                }

                for (var i = 0; i < 5; i++) {
                    var d = physics.CarDamage[i] - previous.Physics.CarDamage[i];
                    if (d > 0.1 && (DateTime.Now - _crashTime).TotalSeconds > 5d) {
                        TotalCrashes++;
                        _crashTime = DateTime.Now;
                    }
                }

                /* offroad counter */
                if (_offroad) {
                    _offroad = physics.NumberOfTyresOut > 0;
                } else if (physics.NumberOfTyresOut > 3) {
                    _offroad = true;

                    if ((DateTime.Now - _offroadTime).TotalSeconds > 10) {
                        GoneOffroad++;
                        _offroadTime = DateTime.Now;
                    }
                }

                if (physics.SpeedKmh > OptionMinSpeedKmh) {
                    /* status */
                    var slip = physics.WheelSlip;
                    var prev = previous.Physics.WheelSlip;

                    var e0 = Equals(slip[0], prev[0]);
                    var e1 = Equals(slip[1], prev[1]);
                    var e2 = Equals(slip[2], prev[2]);
                    var e3 = Equals(slip[3], prev[3]);

                    if (e0 && e1) {
                        if (e2 && e3) {
                            CurrentStatus = Status.Airborne;
                        } else if (!e2 && !e3) {
                            CurrentStatus = Status.Wheelie;
                        } else {
                            CurrentStatus = Status.Live;
                        }
                    } else if (e0 && e2 && !e3 || e1 && e3 && !e2) {
                        CurrentStatus = Status.TwoWheels;
                    } else {
                        CurrentStatus = Status.Live;
                    }

                    /* max speed, driven distance and time */
                    if (physics.SpeedKmh > MaxSpeed) MaxSpeed = physics.SpeedKmh;
                    Distance += distance.Length();
                    Time += time;

                    /* burnt fuel */
                    var fuel = previous.Physics.Fuel - current.Physics.Fuel;
                    if (fuel > 0 && fuel < 0.05) {
                        FuelBurnt += fuel;
                    }
                } else {
                    CurrentStatus = Status.Live;
                }
            }
        }
    }
}