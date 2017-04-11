using AcManager.Tools.Profile;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private double? _totalDrivenDistance;

        public double TotalDrivenDistance => _totalDrivenDistance ?? (_totalDrivenDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(Id)).Value;

        public void RaiseTotalDrivenDistanceChanged() {
            _totalDrivenDistance = null;
            OnPropertyChanged(nameof(TotalDrivenDistance));
        }

        private double? _maxSpeedAchieved;

        public double MaxSpeedAchieved => _maxSpeedAchieved ?? (_maxSpeedAchieved = PlayerStatsManager.Instance.GetMaxSpeedByCar(Id)).Value;

        public void RaiseMaxSpeedAchievedChanged() {
            _maxSpeedAchieved = null;
            OnPropertyChanged(nameof(MaxSpeedAchieved));
        }
    }
}
