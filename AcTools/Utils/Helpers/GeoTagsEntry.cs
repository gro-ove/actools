using System;

namespace AcTools.Utils.Helpers {
    public class GeoTagsEntry {
        public readonly string Latitude, Longitude;
        public readonly double LatitudeValue, LongitudeValue;

        public bool IsEmptyOrInvalid { get; }

        public GeoTagsEntry(string lat, string lon) {
            Latitude = lat;
            Longitude = lon;

            IsEmptyOrInvalid = !FlexibleParser.TryParseDouble(Latitude, out LatitudeValue) ||
                    !FlexibleParser.TryParseDouble(Longitude, out LongitudeValue);
            if (IsEmptyOrInvalid) return;

            if (Latitude.ToLower().Contains("s")) {
                LatitudeValue *= -1.0;
            }

            if (Longitude.ToLower().Contains("w")) {
                LongitudeValue *= -1.0;
            }

            if (Math.Abs(LatitudeValue) > 90 || Math.Abs(LongitudeValue) > 180) {
                IsEmptyOrInvalid = true;
            }
        }

        public GeoTagsEntry(double lat, double lon) {
            LatitudeValue = lat;
            LongitudeValue = lon;

            Latitude = $"{lat:F5}° {(lat < 0 ? "S" : "N")}";
            Longitude = $"{lon:F5}° {(lon < 0 ? "W" : "E")}";
        }

        public override string ToString() => IsEmptyOrInvalid ? "" : Latitude + ", " + Longitude;
    }
}
