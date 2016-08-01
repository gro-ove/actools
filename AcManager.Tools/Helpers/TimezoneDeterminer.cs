using System;
using System.Diagnostics;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public static class TimeZoneDeterminer {
        private const string Key = ".TimeZoneDeterminer:";

        public static TimeZoneInfo TryToDetermine(GeoTagsEntry geoTags) {
            var key = Key + geoTags;
            if (ValuesStorage.Contains(key)) {
                var loaded = ValuesStorage.GetTimeZoneInfo(key);
                if (loaded != null) {
                    return ValuesStorage.GetTimeZoneInfo(key);
                }
            }

            var result = GoogleApiProvider.TryToDetermineTimeZone(geoTags);
            if (result == null) return null;

            ValuesStorage.Set(key, result);
            return result;
        }
    }
}
