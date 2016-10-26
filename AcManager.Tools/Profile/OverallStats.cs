using System;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Profile {
    public partial class PlayerStatsManager {
        public class OverallStats : NotifyPropertyChanged {
            private double _maxDistancePerCar;
            private string _maxDistancePerCarCarId;
            private double _maxDistancePerTrack;
            private string _maxDistancePerTrackTrackId;
            private double _maxSpeed;
            private string _maxSpeedCarId;
            private double _longestAirborne;
            private string _longestAirborneCarId;
            private double _longestWheelie;
            private string _longestWheelieCarId;
            private double _longestTwoWheels;
            private string _longestTwoWheelsCarId;
            private double _distance;
            private double _fuelBurnt;
            private TimeSpan _time;
            private int _goneOffroadTimes;
            private double _totalAirborne;
            private double _totalWheelie;
            private double _totalTwoWheels;

            public OverallStats(Storage associatedStorage = null) {
                _associatedStorage = associatedStorage ?? new Storage();
            }

            [JsonIgnore]
            private readonly Storage _associatedStorage;

            [JsonProperty]
            public int Version => 2;

            /* extremums */
            [JsonProperty]
            public double MaxDistancePerCar {
                get { return _maxDistancePerCar; }
                internal set {
                    if (value.Equals(_maxDistancePerCar)) return;
                    _maxDistancePerCar = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MaxDistancePerCarKm));
                    OnPropertyChanged(nameof(MaxDistancePerCarSame));
                }
            }

            [JsonProperty]
            public string MaxDistancePerCarCarId {
                get { return _maxDistancePerCarCarId; }
                internal set {
                    if (value == _maxDistancePerCarCarId) return;
                    _maxDistancePerCarCarId = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double MaxDistancePerTrack {
                get { return _maxDistancePerTrack; }
                internal set {
                    if (value.Equals(_maxDistancePerTrack)) return;
                    _maxDistancePerTrack = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MaxDistancePerTrackKm));
                    OnPropertyChanged(nameof(MaxDistancePerTrackSame));
                }
            }

            [JsonProperty]
            public string MaxDistancePerTrackTrackId {
                get { return _maxDistancePerTrackTrackId; }
                internal set {
                    if (value == _maxDistancePerTrackTrackId) return;
                    _maxDistancePerTrackTrackId = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double MaxSpeed {
                get { return _maxSpeed; }
                internal set {
                    if (value.Equals(_maxSpeed)) return;
                    _maxSpeed = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public string MaxSpeedCarId {
                get { return _maxSpeedCarId; }
                internal set {
                    if (value == _maxSpeedCarId) return;
                    _maxSpeedCarId = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double LongestAirborne {
                get { return _longestAirborne; }
                internal set {
                    if (value.Equals(_longestAirborne)) return;
                    _longestAirborne = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public string LongestAirborneCarId {
                get { return _longestAirborneCarId; }
                internal set {
                    if (value == _longestAirborneCarId) return;
                    _longestAirborneCarId = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double LongestWheelie {
                get { return _longestWheelie; }
                internal set {
                    if (value.Equals(_longestWheelie)) return;
                    _longestWheelie = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public string LongestWheelieCarId {
                get { return _longestWheelieCarId; }
                internal set {
                    if (value == _longestWheelieCarId) return;
                    _longestWheelieCarId = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double LongestTwoWheels {
                get { return _longestTwoWheels; }
                internal set {
                    if (value.Equals(_longestTwoWheels)) return;
                    _longestTwoWheels = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public string LongestTwoWheelsCarId {
                get { return _longestTwoWheelsCarId; }
                internal set {
                    if (value == _longestTwoWheelsCarId) return;
                    _longestTwoWheelsCarId = value;
                    OnPropertyChanged();
                }
            }

            /* summary */
            /// <summary>
            /// Meters.
            /// </summary>
            [JsonProperty]
            public double Distance {
                get { return _distance; }
                internal set {
                    if (value.Equals(_distance)) return;
                    _distance = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AverageSpeed));
                    OnPropertyChanged(nameof(FuelConsumption));
                    OnPropertyChanged(nameof(DistanceKm));
                    OnPropertyChanged(nameof(MaxDistancePerCarSame));
                    OnPropertyChanged(nameof(MaxDistancePerTrackSame));
                }
            }

            /// <summary>
            /// Liters.
            /// </summary>
            [JsonProperty]
            public double FuelBurnt {
                get { return _fuelBurnt; }
                internal set {
                    if (value.Equals(_fuelBurnt)) return;
                    _fuelBurnt = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FuelConsumption));
                }
            }

            [JsonProperty]
            public TimeSpan Time {
                get { return _time; }
                internal set {
                    if (value.Equals(_time)) return;
                    _time = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AverageSpeed));
                }
            }

            [JsonProperty]
            public int GoneOffroadTimes {
                get { return _goneOffroadTimes; }
                internal set {
                    if (value == _goneOffroadTimes) return;
                    _goneOffroadTimes = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double TotalAirborne {
                get { return _totalAirborne; }
                internal set {
                    if (value.Equals(_totalAirborne)) return;
                    _totalAirborne = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double TotalWheelie {
                get { return _totalWheelie; }
                internal set {
                    if (value.Equals(_totalWheelie)) return;
                    _totalWheelie = value;
                    OnPropertyChanged();
                }
            }

            [JsonProperty]
            public double TotalTwoWheels {
                get { return _totalTwoWheels; }
                internal set {
                    if (value.Equals(_totalTwoWheels)) return;
                    _totalTwoWheels = value;
                    OnPropertyChanged();
                }
            }

            private double _totalTyreWear;

            [JsonProperty]
            public double TotalTyreWear {
                get { return _totalTyreWear; }
                set {
                    if (Equals(value, _totalTyreWear)) return;
                    _totalTyreWear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalTyreWearRounded));
                }
            }

            private int _totalCrashes;

            [JsonProperty]
            public int TotalCrashes {
                get { return _totalCrashes; }
                set {
                    if (Equals(value, _totalCrashes)) return;
                    _totalCrashes = value;
                    OnPropertyChanged();
                }
            }

            /* calculated on-fly */
            [JsonIgnore]
            public double DistanceKm => Distance / 1e3;

            [JsonIgnore]
            public bool MaxDistancePerCarSame => Math.Abs(Distance - MaxDistancePerCar) < 0.1;

            [JsonIgnore]
            public bool MaxDistancePerTrackSame => Math.Abs(Distance - MaxDistancePerTrack) < 0.1;

            [JsonIgnore]
            public double MaxDistancePerCarKm => MaxDistancePerCar / 1e3;

            [JsonIgnore]
            public double MaxDistancePerTrackKm => MaxDistancePerTrack / 1e3;

            [JsonIgnore]
            public double AverageSpeed => Equals(Time.TotalHours, 0d) ? 0d : DistanceKm / Time.TotalHours;

            [JsonIgnore]
            public double FuelConsumption => Equals(DistanceKm, 0d) ? 0d : 1e2 * FuelBurnt / DistanceKm;

            [JsonIgnore]
            public double TotalTyreWearRounded => Math.Floor(TotalTyreWear / 0.8);

            public void Extend(SessionStats session) {
                /* extremums */
                {
                    var drivenDistance = _associatedStorage.GetDouble(KeyDistancePerCarPrefix + session.CarId) + session.Distance;
                    _associatedStorage.Set(KeyDistancePerCarPrefix + session.CarId, drivenDistance);

                    if (session.CarId == MaxDistancePerCarCarId) {
                        MaxDistancePerCar += session.Distance;
                    } else if (drivenDistance > MaxDistancePerCar) {
                        MaxDistancePerCar = drivenDistance;
                        MaxDistancePerCarCarId = session.CarId;
                    }
                }

                {
                    var drivenDistance = _associatedStorage.GetDouble(KeyDistancePerTrackPrefix + session.TrackId) + session.Distance;
                    _associatedStorage.Set(KeyDistancePerTrackPrefix + session.TrackId, drivenDistance);

                    if (session.TrackId == MaxDistancePerTrackTrackId) {
                        MaxDistancePerTrack += session.Distance;
                    } else if (drivenDistance > MaxDistancePerTrack) {
                        MaxDistancePerTrack = drivenDistance;
                        MaxDistancePerTrackTrackId = session.TrackId;
                    }
                }

                if (session.MaxSpeed > MaxSpeed) {
                    MaxSpeed = session.MaxSpeed;
                    MaxSpeedCarId = session.CarId;
                }

                if (session.LongestAirborne > LongestAirborne) {
                    LongestAirborne = session.LongestAirborne;
                    LongestAirborneCarId = session.CarId;
                }

                if (session.LongestWheelie > LongestWheelie) {
                    LongestWheelie = session.LongestWheelie;
                    LongestWheelieCarId = session.CarId;
                }

                if (session.LongestTwoWheels > LongestTwoWheels) {
                    LongestTwoWheels = session.LongestTwoWheels;
                    LongestTwoWheelsCarId = session.CarId;
                }

                /* summary */
                Distance += session.Distance;
                FuelBurnt += session.FuelBurnt;
                Time += session.Time;
                GoneOffroadTimes += session.GoneOffroad;
                TotalAirborne += session.TotalAirborne;
                TotalWheelie += session.TotalWheelie;
                TotalTwoWheels += session.TotalTwoWheels;
                TotalTyreWear += session.TotalTyreWear;
                TotalCrashes += session.TotalCrashes;
            }

            public void CopyFrom(OverallStats updated) {
                foreach (var p in typeof(OverallStats).GetProperties().Where(p => p.CanWrite)) {
                    p.SetValue(this, p.GetValue(updated, null), null);
                }
            }
        }
    }
}