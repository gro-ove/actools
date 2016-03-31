using System;
using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.Processes {
    public partial class Game {
        public class Result {
            [JsonProperty(PropertyName = "track")]
            public string TrackId;

            [JsonProperty(PropertyName = "players")]
            public ResultPlayer[] Players;

            [JsonProperty(PropertyName = "number_of_sessions")]
            public int NumberOfSessions;

            [JsonProperty(PropertyName = "sessions")]
            public ResultSession[] Sessions;

            [JsonProperty(PropertyName = "extras")]
            [JsonConverter(typeof(ResultExtraConverter))]
            public ResultExtra[] Extras;

            public T GetExtraByType<T>() where T : ResultExtra {
                return Extras.OfType<T>().FirstOrDefault();
            }

            public bool IsNotCancelled => NumberOfSessions == Sessions.Length && (Sessions.Any(x => x.IsNotCancelled) || Extras.Any(x => x.IsNotCancelled));
        }

        public class ResultExtra {
            [JsonProperty(PropertyName = "name")]
            public string Name;

            public virtual bool IsNotCancelled => false;
        }

        public class ResultExtraBestLap : ResultExtra {
            [JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            [JsonProperty(PropertyName = "time")]
            public TimeSpan Time;

            public override bool IsNotCancelled => Time != TimeSpan.Zero;
        }

        public class ResultExtraSpecialEvent : ResultExtra {
            [JsonProperty(PropertyName = "guid")]
            public string Guid;

            [JsonProperty(PropertyName = "max")]
            public int Max;

            [JsonProperty(PropertyName = "tier")]
            public int Tier;

            public override bool IsNotCancelled => Guid != string.Empty || Max != 0 || Tier != -1;
        }

        public class ResultExtraTimeAttack : ResultExtra {
            [JsonProperty(PropertyName = "points")]
            public int Points;

            public override bool IsNotCancelled => Points > 0;
        }

        public class ResultExtraDrift : ResultExtra {
            [JsonProperty(PropertyName = "points")]
            public int Points;

            [JsonProperty(PropertyName = "combos")]
            public int MaxCombo;

            [JsonProperty(PropertyName = "levels")]
            public int MaxLevel;

            public override bool IsNotCancelled => Points > 0 || MaxCombo != 0 || MaxLevel != 0;
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
                    var doubleValue = double.Parse(value.ToString());
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
            [JsonProperty(PropertyName = "name")]
            public string Name;

            [JsonProperty(PropertyName = "car")]
            public string CarId;

            [JsonProperty(PropertyName = "skin")]
            public string CarSkinId;
        }

        public class ResultLap {
            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "lap")]
            public int LapId;

            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "car")]
            public int CarNumber;
            
            [JsonProperty(PropertyName = "sectors")]
            [JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            public TimeSpan[] SectorsTime;

            /// <summary>
            /// Milliseconds.
            /// </summary>
            [JsonProperty(PropertyName = "time")]
            [JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            public TimeSpan Time;
        }

        public class ResultBestLap {
            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "lap")]
            public int LapId;

            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "car")]
            public int CarNumber;

            /// <summary>
            /// Milliseconds.
            /// </summary>
            [JsonProperty(PropertyName = "time")]
            [JsonConverter(typeof(MillisecondsToTimeSpanConverter))]
            public TimeSpan Time;
        }

        public class ResultSession {
            [JsonProperty(PropertyName = "name")]
            public string Name;

            /// <summary>
            /// Starts from 0.
            /// </summary>
            [JsonProperty(PropertyName = "event")]
            public int EventId;

            [JsonProperty(PropertyName = "type")]
            public SessionType Type;

            [JsonProperty(PropertyName = "lapsCount")]
            public int LapsCount;

            /// <summary>
            /// Minutes.
            /// </summary>
            [JsonProperty(PropertyName = "duration")]
            [JsonConverter(typeof(MinutesToTimeSpanConverter))]
            public TimeSpan Duration;

            /// <summary>
            /// [Car ID] = Laps
            /// </summary>
            [JsonProperty(PropertyName = "laps")]
            public ResultLap[] Laps;

            [JsonProperty(PropertyName = "lapstotal")]
            public int[] LapsTotalPerCar;

            [JsonProperty(PropertyName = "bestLaps")]
            public ResultBestLap[] BestLaps;

            /// <summary>
            /// [Place] = Car ID
            /// </summary>
            [JsonProperty(PropertyName = "raceResult")]
            [CanBeNull]
            public int[] CarPerTakenPlace;

            public bool IsNotCancelled {
                get {
                    switch (Type) {
                        case SessionType.Practice:
                            return true;

                        case SessionType.Qualification:
                        case SessionType.Race:
                            return LapsTotalPerCar.Contains(LapsCount);

                        case SessionType.Drift:
                        case SessionType.Hotlap:
                        case SessionType.TimeAttack:
                            return LapsTotalPerCar.FirstOrDefault() > 0;
                    
                        case SessionType.Booking:
                            return true;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            /// <summary>
            /// Eighter a racing place or a place based on best lap time.
            /// </summary>
            /// <returns>[Place] = Car ID (starts with 0)</returns>
            public int[] GetCarPerTakenPlace() {
                return CarPerTakenPlace ?? BestLaps
                        .GroupBy(x => x.CarNumber)
                        .Select(x => x.MinEntryOrDefault(y => y.Time))
                        .OrderBy(x => x.Time)
                        .Select(x => x.CarNumber).ToArray();
            }

            /// <summary>
            /// Eighter a racing place or a place based on best lap time.
            /// </summary>
            /// <returns>[Car ID] = Place (starts with 0!)</returns>
            public int[] GetTakenPlacesPerCar() {
                return GetCarPerTakenPlace().Select((x, i) => new[] { x, i }).OrderBy(x => x[0]).Select(x => x[1]).ToArray();
            }
        }
    }
}
