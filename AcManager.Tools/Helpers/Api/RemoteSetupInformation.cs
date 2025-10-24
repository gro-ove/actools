using System;
using System.Globalization;
using System.Linq;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
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
    }
}