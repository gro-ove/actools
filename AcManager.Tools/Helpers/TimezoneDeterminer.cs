using System;
using System.Net;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class TimeZoneDeterminer {
        private const string Key = ".TimeZoneDeterminer:";

        [ItemCanBeNull]
        public static async Task<TimeZoneInfo> TryToDetermineAsync(GeoTagsEntry geoTags) {
            var key = Key + geoTags;
            if (CacheStorage.Contains(key)) {
                return CacheStorage.GetTimeZoneInfo(key);
            }

            try {
                var result = await GoogleApiProvider.DetermineTimeZoneAsync(geoTags);
                CacheStorage.Set(key, result);
                return result;
            } catch (WebException e) {
                Logging.Warning(e.Message);
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                CacheStorage.Set(key, "");
                return null;
            }
        }
    }
}
