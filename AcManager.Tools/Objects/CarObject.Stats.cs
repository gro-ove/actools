using System.Windows;
using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private double? _totalDrivenDistance;

        /// <summary>
        /// Meters!
        /// </summary>
        public double TotalDrivenDistance => _totalDrivenDistance ?? (_totalDrivenDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(Id)).Value;

        public double TotalDrivenDistanceKm => TotalDrivenDistance / 1e3;

        public void RaiseTotalDrivenDistanceChanged() {
            _totalDrivenDistance = null;
            OnPropertyChanged(nameof(TotalDrivenDistance));
            OnPropertyChanged(nameof(TotalDrivenDistanceKm));
        }

        private double? _maxSpeedAchieved;

        public double MaxSpeedAchieved => _maxSpeedAchieved ?? (_maxSpeedAchieved = PlayerStatsManager.Instance.GetMaxSpeedByCar(Id)).Value;

        public void RaiseMaxSpeedAchievedChanged() {
            _maxSpeedAchieved = null;
            OnPropertyChanged(nameof(MaxSpeedAchieved));
        }

        private AsyncCommand _clearStatsCommand;

        public AsyncCommand ClearStatsCommand => _clearStatsCommand ?? (_clearStatsCommand = new AsyncCommand(async () => {
            if (MessageDialog.Show("Are you sure you want to delete all sessions with this car from stats?", "Careful, please", MessageDialogButton.YesNo)
                    != MessageBoxResult.Yes) return;
            using (WaitingDialog.Create("Clearing and rebuilding…")) {
                await PlayerStatsManager.Instance.RemoveCarAsync(Id);
                RaiseMaxSpeedAchievedChanged();
                RaiseTotalDrivenDistanceChanged();
            }
        }));

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