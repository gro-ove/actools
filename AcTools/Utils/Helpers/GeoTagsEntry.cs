using System;

namespace AcTools.Utils.Helpers {
    public class GeoTagsEntry {
        public readonly string Latitude, Longitude;
        public readonly double? LatitudeValue, LongitudeValue;

        public bool IsEmptyOrInvalid { get; }

        public GeoTagsEntry(string lat, string lng) {
            Latitude = lat;
            Longitude = lng;

            LatitudeValue = FlexibleParser.TryParseDouble(Latitude);
            LongitudeValue = FlexibleParser.TryParseDouble(Longitude);

            if (LatitudeValue.HasValue && Math.Abs(LatitudeValue.Value) > 90d) {
                LatitudeValue = null;
            }

            if (LongitudeValue.HasValue && Math.Abs(LongitudeValue.Value) > 180d) {
                LongitudeValue = null;
            }

            IsEmptyOrInvalid = !LatitudeValue.HasValue || !LongitudeValue.HasValue;
            if (IsEmptyOrInvalid) return;

            if (Latitude.ToLower().Contains("s")) {
                LatitudeValue *= -1d;
            }

            if (Longitude.ToLower().Contains("w")) {
                LongitudeValue *= -1d;
            }
        }

        public GeoTagsEntry(double lat, double lng) {
            LatitudeValue = lat;
            LongitudeValue = lng;

            Latitude = $"{lat:F4}° {(lat < 0d ? "S" : "N")}";
            Longitude = $"{lng:F4}° {(lng < 0d ? "W" : "E")}";
        }

        public override string ToString() => IsEmptyOrInvalid ? "" : Latitude + ", " + Longitude;

        public static string ToLat(double lat) => $"{lat:F4}° {(lat < 0 ? "S" : "N")}";

        public static string ToLng(double lng) => $"{lng:F4}° {(lng < 0 ? "W" : "E")}";
    }
}
