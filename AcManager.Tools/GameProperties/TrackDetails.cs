using System;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class TrackDetails : Game.RaceIniProperties {
        public override void Set(IniFile file) {
            try {
                var trackId = file["RACE"].GetNonEmpty("TRACK");
                var configurationId = file["RACE"].GetNonEmpty("CONFIG_TRACK");
                var track = TracksManager.Instance.GetLayoutById(trackId ?? string.Empty, configurationId);

                file["LIGHTING"].Remove("__TRACK_GEOTAG_LAT");
                file["LIGHTING"].Remove("__TRACK_GEOTAG_LAT");
                file["LIGHTING"].Remove("__TRACK_TIMEZONE_OFFSET");

                if (track == null) return;

                var trackGeoTags = track.GeoTags;
                if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                    trackGeoTags = TracksLocator.TryToLocateAsync(track).Result;
                    Logging.Write("Track geo tags: " + trackGeoTags);
                }

                file["LIGHTING"].Set("__TRACK_GEOTAG_LAT", trackGeoTags?.LatitudeValue);
                file["LIGHTING"].Set("__TRACK_GEOTAG_LONG", trackGeoTags?.LongitudeValue);

                if (trackGeoTags != null) {
                    var timeZone = TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags).Result;
                    if (timeZone != null) {
                        var dateUnix = file["LIGHTING"].GetLong("__CM_DATE", 0);
                        var date = dateUnix == 0 ? DateTime.Now : dateUnix.ToDateTime();
                        file["LIGHTING"].Set("__TRACK_TIMEZONE_BASE_OFFSET", timeZone.BaseUtcOffset.TotalSeconds);
                        file["LIGHTING"].Set("__TRACK_TIMEZONE_OFFSET", timeZone.GetUtcOffset(date).TotalSeconds);
                        file["LIGHTING"].Set("__TRACK_TIMEZONE_DTS", timeZone.IsDaylightSavingTime(date));
                        Logging.Write("Track time zone: " + timeZone + ", daylight saving time: " + timeZone.IsDaylightSavingTime(date) + ", date: " + date);
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }
    }
}