using System;
using System.Diagnostics;
using System.Linq;
using AcManager.Tools.Objects;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using SlimDX;

namespace AcManager.Tools.Profile {
    public partial class PlayerStatsManager {
        public class SessionStatsEventArgs : EventArgs {
            public SessionStatsEventArgs(SessionStats stats) {
                Stats = stats;
            }

            public SessionStats Stats { get; }
        }

        /// <summary>
        /// Doesn’t spawn PropertyChanged event — don’t bind while game is still running.
        /// </summary>
        public class SessionStats : NotifyPropertyChanged {
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

            #region Actual data
            [JsonProperty]
            public DateTime StartedAt { get; } = DateTime.Now;

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
            #endregion

            private Status _currentStatus;

            // [JsonProperty, JsonConverter(typeof(StringEnumConverter))]
            [JsonIgnore]
            public Status CurrentStatus {
                get { return _currentStatus; }
                internal set {
                    if (Equals(_currentStatus, value)) return;

                    if (value == Status.Live) {
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
                    }

                    _currentStatus = value;
                    _statusStart = _coordinates;
                    _statusStopwatch = Stopwatch.StartNew();
                }
            }

            #region Calculated on-fly
            [JsonIgnore]
            public double DistanceKm => Distance / 1e3;

            [JsonIgnore]
            public double AverageSpeed => Equals(Time.TotalHours, 0d) ? 0d : DistanceKm / Time.TotalHours;

            [JsonIgnore]
            public double FuelConsumption => Equals(DistanceKm, 0d) ? 0d : 1e2 * FuelBurnt / DistanceKm;
            #endregion

            #region Internal status variables
            private bool _offroad, _lapSpoiled, _lapSpoiledId;
            private DateTime _offroadTime, _crashTime;
            private Vector3 _coordinates;
            private Vector3 _statusStart;
            private Stopwatch _statusStopwatch;
            #endregion

#if DEBUG
            [JsonProperty("z")]
            public string Temp;
#endif

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

                var distance = graphics.CarCoordinates - previous.Graphics.CarCoordinates;
                var calcSpeed = distance.Length() / 1e3 / time.TotalHours;

                var bestLap = TimeSpan.FromMilliseconds(graphics.BestTimeInt);
                if (bestLap > TimeSpan.Zero && (!BestLap.HasValue || bestLap < BestLap)) {
                    BestLap = bestLap;
                }

                if (distance.Y < -2.5) {
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

                // Temp = physics.CarDamage.JoinToString(",");

                if (physics.SpeedKmh > 10) {
                    for (var i = 0; i < 4; i++) {
                        var d = previous.Physics.TyreWear[i] - physics.TyreWear[i];
                        if (d > 0 && d < 0.01) {
                            TotalTyreWear += d;
                        }
                    }

                    for (var i = 0; i < 5; i++) {
                        var d = physics.CarDamage[i] - previous.Physics.CarDamage[i];
                        if (d > 0.1 && (DateTime.Now - _crashTime).TotalSeconds > 5d) {
                            TotalCrashes++;
                            _crashTime = DateTime.Now;
                        }
                    }

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
                } else {
                    CurrentStatus = Status.Live;
                }
            }
        }
    }
}