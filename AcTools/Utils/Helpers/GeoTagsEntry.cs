using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AcTools.Utils.Helpers {
    public class GeoTagsEntry {
        public readonly string Latitude, Longitude;
        public readonly string DisplayLatitude, DisplayLongitude;
        public readonly double? LatitudeValue, LongitudeValue;

        public bool IsEmptyOrInvalid { get; }

        protected bool Equals(GeoTagsEntry other) {
            if (other == null) return false;
            return IsEmptyOrInvalid
                    ? other.IsEmptyOrInvalid
                    : !other.IsEmptyOrInvalid && LatitudeValue.Equals(other.LatitudeValue) && LongitudeValue.Equals(other.LongitudeValue);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GeoTagsEntry)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return IsEmptyOrInvalid ? 0 : (LatitudeValue.GetHashCode() * 397) ^ LongitudeValue.GetHashCode();
            }
        }

        public static bool operator ==(GeoTagsEntry a, GeoTagsEntry b) {
            return a?.Equals(b) ?? (object)b == null;
        }

        public static bool operator !=(GeoTagsEntry a, GeoTagsEntry b) {
            return !(a == b);
        }

        private static double? ParseCoordinate(string s, bool latMode) {
            var m = Regex.Matches(s, @"-?\d+([\.,]\d+)?").OfType<Match>().Select(x => FlexibleParser.TryParseDouble(x.Value) ?? 0d).ToList();
            if (m.Count < 1) return null;

            var degrees = m.ElementAtOr(0, 0d);
            var minutes = m.ElementAtOr(1, 0d);
            var seconds = m.ElementAtOr(2, 0d);
            var milliseconds = m.ElementAtOr(3, 0d);
            var sign = degrees >= 0 ? 1 : -1;
            degrees = Math.Abs(degrees);

            if (m.Count == 1) {
                if (degrees > 909090) {
                    milliseconds = degrees;
                    degrees = 0;
                } else if (degrees > 9090) {
                    var newDegrees = Math.Floor(degrees / 10000);
                    minutes = Math.Floor((degrees - newDegrees * 10000) / 100);
                    seconds = Math.Floor(degrees - newDegrees * 10000 - minutes * 100);
                    degrees = newDegrees;
                } else if (degrees > 360) {
                    var newDegrees = Math.Floor(degrees / 100);
                    minutes = degrees - newDegrees * 100;
                    degrees = newDegrees;
                }
            }

            var result = sign * (degrees + minutes / 60 + seconds / 3600 + milliseconds / 3600000);
            if (Math.Abs(result) > (latMode ? 90d : 180d)) return null;

            if (s.ToLower().Contains(latMode ? "s" : "w")) {
                result = -result;
            }

            return result;
        }

        private static string FormatCoordinates(double degrees, bool latMode) {
            var isNegative = degrees < 0;
            degrees = Math.Abs(degrees);

            var deg = (int)Math.Floor(degrees);
            var delta = degrees - deg;

            var seconds = (int)Math.Floor(3600.0 * delta);
            var sec = seconds % 60;

            // var min = (int)Math.Floor(seconds / 60.0);
            // delta = delta * 3600.0 - seconds;
            // var mil = (int)(1000.0 * delta);
            var min = (int)Math.Round(seconds / 60.0);

            var post = latMode ? isNegative ? "S" : "N" : isNegative ? "W" : "E";
            return $"{deg}° {min:00}′ {sec:00}″ {post}";
        }

        public GeoTagsEntry(string lat, string lng) {
            Latitude = lat;
            Longitude = lng;
            LatitudeValue = ParseCoordinate(Latitude, true);
            LongitudeValue = ParseCoordinate(Longitude, false);
            IsEmptyOrInvalid = !LatitudeValue.HasValue || !LongitudeValue.HasValue;

            if (IsEmptyOrInvalid) {
                DisplayLatitude = Latitude;
                DisplayLongitude = Longitude;
            } else {
                DisplayLatitude = FormatCoordinates(LatitudeValue ?? 0d, true);
                DisplayLongitude = FormatCoordinates(LongitudeValue ?? 0d, false);
            }
        }

        public GeoTagsEntry(double lat, double lng) {
            LatitudeValue = lat;
            LongitudeValue = lng;
            DisplayLatitude = Latitude = FormatCoordinates(lat, true);
            DisplayLongitude = Longitude = FormatCoordinates(lng, false);
        }

        public string OriginalString => Latitude.Or("?") + ", " + Longitude.Or("?");
        public override string ToString() => IsEmptyOrInvalid ? "" : DisplayLatitude + ", " + DisplayLongitude;

        public static string ToLat(double lat) => FormatCoordinates(lat, true);
        public static string ToLng(double lng) => FormatCoordinates(lng, false);
    }
}