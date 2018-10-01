using AcManager.Tools.Profile;

namespace AcManager.Tools.Objects {
    public abstract partial class TrackObjectBase {
        private double? _totalDrivenDistance;

        public double TotalDrivenDistance => _totalDrivenDistance ?? (_totalDrivenDistance = PlayerStatsManager.Instance.GetDistanceDrivenAtTrack(IdWithLayout)).Value;
        public double TotalDrivenDistanceKm => TotalDrivenDistance / 1e3;

        public void RaiseTotalDrivenDistanceChanged() {
            OnPropertyChanged(nameof(TotalDrivenDistance));
            OnPropertyChanged(nameof(TotalDrivenDistanceKm));
        }
    }
}
