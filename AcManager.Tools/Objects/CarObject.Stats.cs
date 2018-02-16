using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Profile;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private double? _totalDrivenDistance;

        /// <summary>
        /// Meters!
        /// </summary>
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

        private bool _tsmSetupsCountLoaded;
        private int? _tsmSetupsCount;
        public int? TsmSetupsCount {
            get {
                if (!_tsmSetupsCountLoaded) {
                    _tsmSetupsCountLoaded = true;
                    UpdateTsmSetupsCount();
                }

                return _tsmSetupsCount;
            }
            set => Apply(value, ref _tsmSetupsCount);
        }

        private async void UpdateTsmSetupsCount() {
            TsmSetupsCount = (await TheSetupMarketApiProvider.GetAvailableSetups(Id))?.Count;
        }
    }
}
