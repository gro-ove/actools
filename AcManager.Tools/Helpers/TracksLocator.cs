using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class TracksLocator {
        private const string Key = ".TrackLocator:";

        [ItemCanBeNull]
        public static async Task<GeoTagsEntry> TryToLocateAsync([CanBeNull] string address) {
            if (string.IsNullOrWhiteSpace(address)) return null;

            var key = Key + address;
            if (CacheStorage.Contains(key)) {
                var cache = CacheStorage.GetPointNullable(key);
                return cache.HasValue ? new GeoTagsEntry(cache.Value.X, cache.Value.Y) : null;
            }

            try {
                var result = await YahooApiProvider.LocateAsync(address);
                if (result.LatitudeValue.HasValue && result.LongitudeValue.HasValue) {
                    Logging.Write($"“{address}”, geo tags: ({result})");
                    CacheStorage.Set(key, new Point(result.LatitudeValue.Value, result.LongitudeValue.Value));
                    return result;
                }

                CacheStorage.Set(key, "");
                return null;
            } catch (WebException e) {
                Logging.Warning("TryToLocateAsync(): " + e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning("TryToLocateAsync(): " + e);
                CacheStorage.Set(key, "");
                return null;
            }
        }

        [ItemCanBeNull]
        public static Task<GeoTagsEntry> TryToLocateAsync([CanBeNull] string country, [CanBeNull] string city) {
            return TryToLocateAsync(string.IsNullOrWhiteSpace(country) ? city :
                    string.IsNullOrWhiteSpace(city) ? country : $"{city.Trim()},{country.Trim()}");
        }

        [ItemCanBeNull]
        public static Task<GeoTagsEntry> TryToLocateAsync([NotNull] TrackObjectBase track) {
            return track.Country != null ? TryToLocateAsync(track.Country, track.City) : Task.FromResult<GeoTagsEntry>(null);
        }
    }
}
