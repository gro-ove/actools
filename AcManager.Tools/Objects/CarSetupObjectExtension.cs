using AcManager.Tools.Helpers;

namespace AcManager.Tools.Objects {
    public static class CarSetupObjectExtension {
        public static int CompareTo(this ICarSetupObject l, ICarSetupObject r) {
            if (l == null) return r == null ? 0 : 1;
            if (r == null) return -1;

            var lhsEnabled = l.Enabled;
            if (lhsEnabled != r.Enabled) return lhsEnabled ? -1 : 1;

            var lhsParentId = l.TrackId;
            var rhsParentId = r.TrackId;

            if (lhsParentId == null) return rhsParentId == null ? 0 : -1;
            if (rhsParentId == null) return 1;
            if (lhsParentId == rhsParentId) {
                return l.DisplayName.InvariantCompareTo(r.DisplayName);
            }

            var lhsParent = l.Track;
            var rhsParent = r.Track;
            if (lhsParent == null) return rhsParent == null ? 0 : 1;
            return rhsParent == null ? -1 : lhsParent.CompareTo(rhsParent);
        }
    }
}