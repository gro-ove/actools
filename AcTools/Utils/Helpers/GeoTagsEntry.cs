using System;

namespace AcTools.Utils.Helpers {
    public class GeoTagsEntry {
        public readonly string Latitude, Longitude;
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
                return IsEmptyOrInvalid ? 0 :(LatitudeValue.GetHashCode() * 397) ^ LongitudeValue.GetHashCode();
            }
        }

        public static bool operator ==(GeoTagsEntry a, GeoTagsEntry b) {
            return a?.Equals(b) ?? (object)b == null;
        }

        public static bool operator !=(GeoTagsEntry a, GeoTagsEntry b) {
            return !(a == b);
        }

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
                LatitudeValue = -Math.Abs(LatitudeValue ?? 0d);
            }

            if (Longitude.ToLower().Contains("w")) {
                LongitudeValue = -Math.Abs(LongitudeValue ?? 0d);
            }
        }

        public GeoTagsEntry(double lat, double lng) {
            LatitudeValue = lat;
            LongitudeValue = lng;

            Latitude = $"{Math.Abs(lat):F4}° {(lat < 0d ? "S" : "N")}";
            Longitude = $"{Math.Abs(lng):F4}° {(lng < 0d ? "W" : "E")}";
        }

        public override string ToString() => IsEmptyOrInvalid ? "" : Latitude + ", " + Longitude;

        public static string ToLat(double lat) => $"{Math.Abs(lat):F4}° {(lat < 0 ? "S" : "N")}";

        public static string ToLng(double lng) => $"{Math.Abs(lng):F4}° {(lng < 0 ? "W" : "E")}";
    }
}
