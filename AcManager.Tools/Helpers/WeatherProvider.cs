using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    using CachedEntry = Tuple<DateTime, WeatherDescription>;

    public static class WeatherProvider {
        private static readonly Dictionary<GeoTagsEntry, CachedEntry> LocalCache = new Dictionary<GeoTagsEntry, CachedEntry>();

        private static void CleanUpCache() {
            var limit = DateTime.Now - TimeSpan.FromMinutes(10);
            var toRemoval = LocalCache.Where(x => x.Value.Item1 < limit).Select(x => x.Key).ToList();
            foreach (var key in toRemoval) {
                LocalCache.Remove(key);
            }
        }

        [ItemCanBeNull]
        public static async Task<WeatherDescription> TryToGetWeatherAsync(GeoTagsEntry geoTags) {
            CleanUpCache();

            CachedEntry cached;
            if (LocalCache.TryGetValue(geoTags, out cached)) {
                return cached.Item2;
            }

            try {
                var result = await new OpenWeatherApiProvider().GetWeatherAsync(geoTags);
                LocalCache.Add(geoTags, new CachedEntry(DateTime.Now, result));
                return result;
            } catch (WebException e) {
                Logging.Warning("[WeatherProvider] TryToGetWeather(): " + e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning("[WeatherProvider] TryToGetWeather(): " + e);
                return null;
            }
        }
    }
}
