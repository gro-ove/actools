using System;
using System.Globalization;
using System.Linq;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api {
    public class RemoteSetupInformation : Displayable {
        [NotNull]
        public string Id { get; }

        [NotNull]
        public string FileName { get; }

        [NotNull]
        public string CarId { get; }

        [CanBeNull]
        public string TrackKunosId { get; }

        [CanBeNull]
        public string Author { get; }

        [CanBeNull]
        public string Version { get; }

        [CanBeNull]
        public string Url { get; }

        [CanBeNull]
        public string Trim { get; }

        public int Downloads { get; }
        public DateTime? AddedDateTime { get; }
        public double? CommunityRating { get; }
        public TimeSpan? BestTime { get; }

        public RemoteSetupInformation([NotNull] string id, string fileName, int downloads, DateTime? addedDateTime, [NotNull] string carId,
                [CanBeNull] string trackKunosId, string author, [CanBeNull] string version, double? communityRating, string trim, TimeSpan? bestTime,
                [CanBeNull] string url) {
            Id = id;
            FileName = fileName ?? id + CarSetupObject.FileExtension;
            Downloads = downloads;
            AddedDateTime = addedDateTime;
            CarId = carId;
            TrackKunosId = trackKunosId;
            Author = author;
            CommunityRating = communityRating;
            Trim = trim;
            BestTime = bestTime;
            Url = url;
            Version = version;
        }

        private static TimeSpan? TryParse(string value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var p = value.Split(new[] { ':', '.' }, StringSplitOptions.RemoveEmptyEntries);

            try {
                double result;
                switch (p.Length) {
                    case 0:
                        return null;
                    case 1:
                        result = double.Parse(p[0], CultureInfo.InvariantCulture);
                        break;
                    case 2:
                        result = double.Parse(p[0], CultureInfo.InvariantCulture) * 60 + double.Parse(p[1], CultureInfo.InvariantCulture);
                        break;
                    case 3:
                        result = (double.Parse(p[0], CultureInfo.InvariantCulture) * 60 + double.Parse(p[1], CultureInfo.InvariantCulture)) * 60 +
                                double.Parse(p[2], CultureInfo.InvariantCulture);
                        break;
                    default:
                        result = ((double.Parse(p[0], CultureInfo.InvariantCulture) * 24 + double.Parse(p[1], CultureInfo.InvariantCulture)) * 60 +
                                double.Parse(p[2], CultureInfo.InvariantCulture)) * 60 + double.Parse(p[3], CultureInfo.InvariantCulture);
                        break;
                }

                return TimeSpan.FromSeconds(result);
            } catch (Exception) {
                return null;
            }
        }

        [CanBeNull]
        public static RemoteSetupInformation FromTheSetupMarketJToken(JToken token, string targetCarId = null) {
            var o = token as JObject;
            if (o == null) return null;

            if ((string)o["sim"]?["code"] != "ac") return null;

            var carId = (string)o["car"]?["ac_code"];
            if (string.IsNullOrEmpty(carId) || targetCarId != null && carId != targetCarId) {
                return null;
            }

            var id = (string)o["_id"];
            if (string.IsNullOrEmpty(id)) {
                return null;
            }

            var trackId = (string)o["track"]?["ac_code"];
            if (string.IsNullOrWhiteSpace(trackId)) trackId = null;

            var url = $@"http://thesetupmarket.com/#/setups/Assetto%20Corsa/{(string)o["author"]?["_id"]}/{id}";
            var ratings = o["ratings"] as JArray;
            return new RemoteSetupInformation(id,
                    (string)o["file_name"], ((string)o["downloads"]).As<int>(),
                    DateTime.TryParse((string)o["added_date"]?["timestamp"] ?? "", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var v) ? v : (DateTime?)null,
                    carId, trackId,
                    (string)o["author"]?["display_name"],
                    (string)o["version"],
                    ratings?.Count > 0 ? ratings.Average(x => ((string)x["rating"]).As<double>()) : (double?)null,
                    (string)o["type"],
                    TryParse((string)o["best_time"]),
                    url);
        }
    }
}