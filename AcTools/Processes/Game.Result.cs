using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.Processes {
    public partial class Game {
        public class Result {
            [JsonProperty(PropertyName = "track"), CanBeNull]
            public string TrackId { get; set; }

            [JsonProperty(PropertyName = "players"), CanBeNull]
            public ResultPlayer[] Players { get; set; }

            [JsonProperty(PropertyName = "number_of_sessions")]
            public int NumberOfSessions { get; set; }

            [JsonProperty(PropertyName = "sessions"), CanBeNull]
            public ResultSession[] Sessions { get; set; }

            [JsonProperty(PropertyName = "extras"), JsonConverter(typeof(ResultExtraConverter)), CanBeNull]
            public ResultExtra[] Extras { get; set; }

            [CanBeNull]
            public T GetExtraByType<T>() where T : ResultExtra {
                return Extras?.OfType<T>().FirstOrDefault();
            }

            public bool GetExtraByType<T>(out T value) where T : ResultExtra {
                value = Extras?.OfType<T>().FirstOrDefault();
                return value != null;
            }

            public bool IsNotCancelled => NumberOfSessions == Sessions?.Length &&
                    (Sessions.Any(x => x.IsNotCancelled) || Extras?.Any(x => x.IsNotCancelled) == true);

            public string GetDescription() {
                return $"(Track={TrackId}, Sessions={NumberOfSessions}, " +
                        $"Players=[{Players?.Select(x => x.GetDescription()).JoinToString(", ")}], " +
                        $"Sessions=[{Sessions?.Select(x => x.GetDescription()).JoinToString(", ")}], " +
                        $"Extras=[{Extras?.Select(x => x.GetDescription()).JoinToString(", ")}])";
            }
        }

        public class ResultExtra {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            public bool IsCancelled => !IsNotCancelled;

            public virtual bool IsNotCancelled => false;

            public virtual string GetDescription() {
                return $"({Name}, Cancelled={!IsNotCancelled})";
            }
        }

        public class ResultExtraBestLap : ResultExtra {
            [JsonConverter(typeof(MillisecondsToTimeSpanConverter)), JsonProperty(PropertyName = "time")]
            public TimeSpan Time { get; set; }

            public override bool IsNotCancelled => Time != TimeSpan.Zero;

            public override string GetDescription() {
                return $"({Name}, Cancelled={!IsNotCancelled}, Time={Time})";
            }
        }

        public class ResultExtraSpecialEvent : ResultExtra {
            [JsonProperty(PropertyName = "guid")]
            public string Guid { get; set; }

            [JsonProperty(PropertyName = "max")]
            public int Max { get; set; }

            [JsonProperty(PropertyName = "tier")]
            public int Tier { get; set; }

            public override bool IsNotCancelled => Guid != string.Empty || Max != 0 || Tier != -1;

            public override string GetDescription() {
                return $"({Name}, Cancelled={!IsNotCancelled}, Guid={Guid}, Tier={Tier}, Max={Max})";
            }
        }

        public class ResultExtraTimeAttack : ResultExtra {
            [JsonProperty(PropertyName = "points")]
            public int Points { get; set; }

            public override bool IsNotCancelled => Points > 0;

            public override string GetDescription() {
                return $"({Name}, Cancelled={!IsNotCancelled}, Points={Points})";
            }
        }

        public class ResultExtraDrift : ResultExtra {
            [JsonProperty(PropertyName = "points")]
            public int Points { get; set; }

            [JsonProperty(PropertyName = "combos")]
            public int MaxCombo { get; set; }

            [JsonProperty(PropertyName = "levels")]
            public int MaxLevel { get; set; }

            public override bool IsNotCancelled => Points > 0 || MaxCombo != 0 || MaxLevel != 0;

            public override string GetDescription() {
                return $"({Name}, Cancelled={!IsNotCancelled}, Points={Points})";
            }
        }

        public class ResultExtraDrag : ResultExtra {
            [JsonProperty(PropertyName = "total")]
            public int Total { get; set; }

            [JsonProperty(PropertyName = "wins")]
            public int Wins { get; set; }

            [JsonProperty(PropertyName = "runs")]
            public int Runs { get; set; }

            [JsonConverter(typeof(MillisecondsToTimeSpanConverter)), JsonProperty(PropertyName = "reaction_time")]
            public TimeSpan ReactionTime { get; set; }

            public override bool IsNotCancelled => Runs > 0;

            public override string GetDescription() {
                return $"({Name}, Cancelled={!IsNotCancelled}, Wins/Runs={Wins}/{Runs})";
            }
        }

        internal class ResultExtraConverter : JsonConverter {
            public override bool CanConvert(Type type) => true;

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                return JArray.Load(reader).Select(jo => {
                    var type = (string)jo["name"];
                    switch (type) {
                        case "drift":
                            return jo.ToObject<ResultExtraDrift>(serializer);

                        case "drag":
                            return jo.ToObject<ResultExtraDrag>(serializer);

                        case "bestlap":
                            return jo.ToObject<ResultExtraBestLap>(serializer);

                        case "special_event":
                            return jo.ToObject<ResultExtraSpecialEvent>(serializer);

                        case "timeattack":
                            return jo.ToObject<ResultExtraTimeAttack>(serializer);

                        default:
                            return jo.ToObject<ResultExtra>(serializer);
                    }
                }).ToArray();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
                throw new NotSupportedException();
            }
        }

        internal abstract class JsonTimeSpanConverter : JsonConverter {
            public override bool CanConvert(Type type) => true;

            public override bool CanWrite => false;

            protected abstract TimeSpan FromValue(double value);

            private TimeSpan Convert(object value) {
                if (value == null) return TimeSpan.Zero;
                try {
                    var doubleValue = value as double? ?? double.Parse(value.ToString(), CultureInfo.InvariantCulture);
                    return doubleValue < 0 ? TimeSpan.Zero : FromValue(doubleValue);
                } catch (Exception) {
                    return TimeSpan.Zero;
                }
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
                return reader.TokenType == JsonToken.StartArray ? (object)JArray.Load(reader).Select(Convert).ToArray() :
                        Convert(reader.Value);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
                throw new NotSupportedException();
            }
        }

        internal class MillisecondsToTimeSpanConverter : JsonTimeSpanConverter {
            protected override TimeSpan FromValue(double value) => TimeSpan.FromMilliseconds(value);
        }

        internal class MinutesToTimeSpanConverter : JsonTimeSpanConverter {
            protected override TimeSpan FromValue(double value) => TimeSpan.FromMinutes(value);
        }

        public class ResultPlayer {
            [JsonProperty("name"), CanBeNull]
            public string Name { get; set; }

            [JsonProperty("car"), CanBeNull]
            public string CarId { get; set; }

            [JsonProperty("skin"), CanBeNull]
            public string CarSkinId { get; set; }

            public string GetDescription() {
                return $"({Name} in {CarId})";
            }

            [JsonConstructor]
            public ResultPlayer(string name, string car, string skin) {
                if (FakeCarIds.IsFake(car, out var f)) {
                    car = f;
                }

                Name = name;
                CarId = car;
                CarSkinId = skin;
            }
        }

        public class ResultLap {
            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "lap")]
            public int LapId { get; set; }

            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "car")]
            public int CarNumber { get; set; }

            [JsonProperty(PropertyName = "cuts")]
            public int Cuts { get; set; }

            [JsonProperty(PropertyName = "tyre"), CanBeNull]
            public string TyresShortName { get; set; }

            [JsonProperty(PropertyName = "sectors"), JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            public TimeSpan[] SectorsTime { get; set; }

            /// <summary>
            /// Milliseconds.
            /// </summary>
            [JsonProperty(PropertyName = "time"), JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            public TimeSpan Time { get; set; }
        }

        public class ResultBestLap {
            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "lap")]
            public int LapId { get; set; }

            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "car")]
            public int CarNumber { get; set; }

            /// <summary>
            /// Milliseconds.
            /// </summary>
            [JsonProperty(PropertyName = "time"), JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            public TimeSpan Time { get; set; }
        }

        public class ResultSession {
            [JsonProperty(PropertyName = "name"), CanBeNull]
            public string Name { get; set; }

            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "event")]
            public int EventId { get; set; }

            [JsonProperty(PropertyName = "type")]
            public SessionType Type { get; set; }

            [JsonProperty(PropertyName = "lapsCount")]
            public int LapsCount { get; set; }

            /// <summary>
            /// Minutes.
            /// </summary>
            [JsonProperty(PropertyName = "duration"), JsonConverter(typeof(MinutesToTimeSpanConverter))]
            public TimeSpan Duration { get; set; }

            /// <summary>
            /// [Car ID] = Laps
            /// </summary>
            [JsonProperty(PropertyName = "laps"), CanBeNull]
            public ResultLap[] Laps { get; set; }

            [JsonProperty(PropertyName = "lapstotal"), CanBeNull]
            public int[] LapsTotalPerCar { get; set; }

            [JsonProperty(PropertyName = "bestLaps"), CanBeNull]
            public ResultBestLap[] BestLaps { get; set; }

            /// <summary>
            /// [Place] = Car ID
            /// </summary>
            [JsonProperty(PropertyName = "raceResult"), CanBeNull]
            public int[] CarPerTakenPlace { get; set; }

            public bool IsNotCancelled {
                get {
                    switch (Type) {
                        case SessionType.Practice:
                            return true;

                        case SessionType.Qualification:
                        case SessionType.Race:
                            return LapsTotalPerCar?.Contains(LapsCount) == true;

                        case SessionType.Drift:
                        case SessionType.Hotlap:
                        case SessionType.TimeAttack:
                            return LapsTotalPerCar?.FirstOrDefault() > 0;

                        case SessionType.Booking:
                            return true;

                        case SessionType.Drag:
                            return LapsTotalPerCar?.FirstOrDefault() > 0;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            /// <summary>
            /// Eighter a racing place or a place based on best lap time.
            /// </summary>
            /// <returns>[Place] = Car ID (starts with 0)</returns>
            [CanBeNull]
            public int[] GetCarPerTakenPlace() {
                return CarPerTakenPlace ?? BestLaps?
                        .GroupBy(x => x.CarNumber)
                        .Select(x => x.MinEntryOrDefault(y => y.Time))
                        .NonNull()
                        .OrderBy(x => x.Time)
                        .Select(x => x.CarNumber).ToArray();
            }

            /// <summary>
            /// Eighter a racing place or a place based on best lap time.
            /// </summary>
            /// <returns>[Car ID] = Place (starts with 0!)</returns>
            [CanBeNull]
            public int[] GetTakenPlacesPerCar() {
                return GetCarPerTakenPlace()?.Select((x, i) => new[] { x, i }).OrderBy(x => x[0]).Select(x => x[1]).ToArray();
            }

            public string GetDescription() {
                return $"({Name}, Laps={LapsCount}, Places=[{CarPerTakenPlace?.JoinToString(", ")}])";
            }
        }
    }
}