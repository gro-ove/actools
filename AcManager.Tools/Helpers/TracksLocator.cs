using System.Windows;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class TracksLocator {
        private const string Key = ".TrackLocator:";

        [CanBeNull]
        public static GeoTagsEntry TryToLocate([CanBeNull] string address) {
            if (string.IsNullOrWhiteSpace(address)) return null;

            var key = Key + address;
            var cache = ValuesStorage.GetPointNullable(key);
            if (cache.HasValue) {
                return new GeoTagsEntry(cache.Value.X, cache.Value.Y);
            }

            var result = YahooApiProvider.TryToLocate(address);
            if (result?.LatitudeValue == null || result.LongitudeValue == null) return null;

            ValuesStorage.Set(key, new Point(result.LatitudeValue.Value, result.LongitudeValue.Value));
            return result;
        }

        [CanBeNull]
        public static GeoTagsEntry TryToLocate([CanBeNull] string country, [CanBeNull] string city) {
            return TryToLocate(string.IsNullOrWhiteSpace(country) ? city :
                    string.IsNullOrWhiteSpace(city) ? country : $"{city.Trim()},{country.Trim()}");
        }

        [CanBeNull]
        public static GeoTagsEntry TryToLocate([NotNull] TrackBaseObject track) {
            return track.Country != null ? TryToLocate(track.Country, track.City) : null;
        }
    }
}
