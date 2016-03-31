using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public static class TracksLocator {
        private const string Key = "__trackslocator_";

        public static GeoTagsEntry TryToLocate(string country, string city) {
            var key = Key + country + "__" + city;
            if (ValuesStorage.Contains(key + "__lat") && ValuesStorage.Contains(key + "__lon")) {
                return new GeoTagsEntry(ValuesStorage.GetDouble(key + "__lat"), ValuesStorage.GetDouble(key + "__lon"));
            }

            var result = new YahooApiProvider().TryToLocate(country, city);
            if (result == null) return null;

            ValuesStorage.Set(key + "__lat", result.LatitudeValue);
            ValuesStorage.Set(key + "__lon", result.LongitudeValue);
            return result;
        }

        public static GeoTagsEntry TryToLocate(TrackBaseObject track) {
            return track.Country != null ? TryToLocate(track.Country, track.City) : null;
        }
    }
}
