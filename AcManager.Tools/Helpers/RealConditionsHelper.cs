using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using TimeZoneConverter;

namespace AcManager.Tools.Helpers {
    public static class RealConditionsHelper {
        public static async Task<string> GetTimezoneId(TrackObjectBase track, CancellationToken cancellation = default) {
            var trackGeoTags = track.GeoTags;
            if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                if (cancellation.IsCancellationRequested) return null;
            }

            var data = DataProvider.Instance.TrackParams[track.MainTrackObject.Id];
            var timeZone = data.ContainsKey(@"TIMEZONE")
                    ? TZConvert.GetTimeZoneInfo(data.GetNonEmpty("TIMEZONE"))
                    : await TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags);
            if (cancellation.IsCancellationRequested) return null;
            return timeZone?.Id;
        }

        public static async Task<TimeSpan> GetTimezoneOffsetAsync(TrackObjectBase track, CancellationToken cancellation = default) {
            var trackGeoTags = track.GeoTags;
            if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                if (cancellation.IsCancellationRequested) return TimeSpan.Zero;
            }

            var data = DataProvider.Instance.TrackParams[track.MainTrackObject.Id];
            var timeZone = data.ContainsKey(@"TIMEZONE")
                    ? TZConvert.GetTimeZoneInfo(data.GetNonEmpty("TIMEZONE"))
                    : await TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags);
            if (cancellation.IsCancellationRequested) return TimeSpan.Zero;
            var utcOffset = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
            return timeZone == null ? TimeSpan.Zero : timeZone.GetUtcOffset(utcOffset) - TimeZoneInfo.Local.GetUtcOffset(utcOffset);
        }

        /// <summary>
        /// Complex method, but it’s the best I can think of for now. Due to async nature,
        /// all results will be returned in callbacks. There is no guarantee in which order callbacks
        /// will be called (and even if they will be called at all or not)!
        /// </summary>
        /// <param name="track">Track for which conditions will be loaded.</param>
        /// <param name="localWeather">Use local weather instead.</param>
        /// <param name="considerTimezones">Consider timezones while setting time. Be careful: you’ll get an unclamped time!</param>
        /// <param name="dateTimeCallback">Set to null if you don’t need an automatic time.</param>
        /// <param name="weatherCallback">Set to null if you don’t need weather.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task UpdateConditionsAsync(TrackObjectBase track, bool localWeather, bool considerTimezones,
                [CanBeNull] Action<DateTime> dateTimeCallback, [CanBeNull] Action<WeatherDescription> weatherCallback, CancellationToken cancellation = default) {
            GeoTagsEntry trackGeoTags = null, localGeoTags = null;

            if (!localWeather || considerTimezones && dateTimeCallback != null) {
                trackGeoTags = track.GeoTags;
                if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                    trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                    if (cancellation.IsCancellationRequested) return;
                }
            }

            if ((trackGeoTags == null || localWeather) && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                localGeoTags = await TracksLocator.TryToLocateAsync(SettingsHolder.Drive.LocalAddress);
                if (cancellation.IsCancellationRequested) return;
            }

            // Time
            var now = DateTime.Now;
            if (dateTimeCallback != null) {
                if (trackGeoTags == null || !considerTimezones) {
                    dateTimeCallback.Invoke(now);
                } else {
                    var data = DataProvider.Instance.TrackParams[track.MainTrackObject.Id];
                    var timeZone = data.ContainsKey(@"TIMEZONE")
                            ? TZConvert.GetTimeZoneInfo(data.GetNonEmpty("TIMEZONE"))
                            : await TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags);
                    if (cancellation.IsCancellationRequested) return;

                    var utcOffset = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
                    var offsetInSeconds = (int)(timeZone == null ? 0 : timeZone.GetUtcOffset(utcOffset).TotalSeconds - TimeZoneInfo.Local.GetUtcOffset(utcOffset).TotalSeconds);
                    dateTimeCallback.Invoke(now + TimeSpan.FromSeconds(offsetInSeconds));
                }
            }

            // Weather
            var tags = localWeather ? localGeoTags : trackGeoTags ?? localGeoTags;
            if (tags == null) return;

            var weather = await WeatherProvider.TryToGetWeatherAsync(tags);
            if (cancellation.IsCancellationRequested) return;

            if (weather != null) {
                weatherCallback?.Invoke(weather);
            }
        }
    }
}