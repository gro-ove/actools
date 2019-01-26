using System.Windows;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.Objects {
    public abstract partial class TrackObjectBase {
        private double? _totalDrivenDistance;

        public double TotalDrivenDistance => _totalDrivenDistance ?? (_totalDrivenDistance = PlayerStatsManager.Instance.GetDistanceDrivenAtTrack(IdWithLayout)).Value;
        public double TotalDrivenDistanceKm => TotalDrivenDistance / 1e3;

        public void RaiseTotalDrivenDistanceChanged() {
            OnPropertyChanged(nameof(TotalDrivenDistance));
            OnPropertyChanged(nameof(TotalDrivenDistanceKm));
        }

        private AsyncCommand _clearStatsCommand;

        public AsyncCommand ClearStatsCommand => _clearStatsCommand ?? (_clearStatsCommand = new AsyncCommand(async () => {
            if (MessageDialog.Show("Are you sure you want to delete all sessions with this layout from stats?", "Careful, please", MessageDialogButton.YesNo)
                    != MessageBoxResult.Yes) return;
            using (WaitingDialog.Create("Clearing and rebuilding…")) {
                await PlayerStatsManager.Instance.RemoveTrackLayoutAsync(IdWithLayout);
                RaiseTotalDrivenDistanceChanged();
            }
        }));

        private AsyncCommand _clearStatsAllCommand;

        public AsyncCommand ClearStatsAllCommand => _clearStatsAllCommand ?? (_clearStatsAllCommand = new AsyncCommand(async () => {
            if (MessageDialog.Show("Are you sure you want to delete all sessions with layouts of this track from stats?", "Careful, please", MessageDialogButton.YesNo)
                    != MessageBoxResult.Yes) return;
            using (WaitingDialog.Create("Clearing and rebuilding…")) {
                await PlayerStatsManager.Instance.RemoveTrackAsync(Id);
                RaiseTotalDrivenDistanceChanged();
            }
        }));
    }
}
