using System.ComponentModel;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Objects {
    [Localizable(false)]
    public class UserChampionshipRound : NotifyPropertyChanged {
        [CanBeNull, JsonProperty("track")]
        public string TrackId { get; }

        [JsonProperty("laps")]
        public int LapsCount { get; }

        [JsonProperty("weather")]
        public int Weather { get; }

        [JsonProperty("surface")]
        public int Surface { get; }

        public static UserChampionshipRound Default => new UserChampionshipRound("magione", 10, 2, 1);

        [JsonConstructor]
        public UserChampionshipRound([CanBeNull] string track, int laps, int weather, int surface) {
            TrackId = track?.Trim();
            LapsCount = laps;
            Weather = weather;
            Surface = surface;
        }

        protected bool Equals(UserChampionshipRound other) {
            return string.Equals(TrackId, other.TrackId) && LapsCount == other.LapsCount && Weather == other.Weather && Surface == other.Surface;
        }

        public override bool Equals(object obj) {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((UserChampionshipRound)obj));
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = TrackId?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ LapsCount;
                hashCode = (hashCode * 397) ^ Weather;
                hashCode = (hashCode * 397) ^ Surface;
                return hashCode;
            }
        }
    }
}