using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Managers;
using AcManager.Tools.Profile;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Miscellaneous {
    public partial class PlayerStats : ILoadableContent, IParametrizedUriContent {
        private string _filter;

        [CanBeNull]
        private Holder<PlayerStatsManager.OverallStats> _stats;

        public void OnUri(Uri uri) {
            _filter = uri.GetQueryParam("Filter");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_filter != null) {
                await CarsManager.Instance.EnsureLoadedAsync();
                if (cancellationToken.IsCancellationRequested) return;

                await TracksManager.Instance.EnsureLoadedAsync();
                if (cancellationToken.IsCancellationRequested) return;
            }

            _stats = _filter == null
                    ? Holder.CreateNonHolding(PlayerStatsManager.Instance.Overall) : await PlayerStatsManager.Instance.GetFilteredAsync(_filter);
            this.OnActualUnload(() => _stats?.Dispose());
        }

        public void Load() {
            _stats = _filter == null ? Holder.CreateNonHolding(PlayerStatsManager.Instance.Overall) : PlayerStatsManager.Instance.GetFiltered(_filter);
            this.OnActualUnload(() => _stats?.Dispose());
        }

        public void Initialize() {
            InitializeComponent();
            if (_stats != null) {
                DataContext = new ViewModel(_stats.Value, _filter);
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public PlayerStatsManager.OverallStats Stats { get; }

            public string Filter { get; }

            public ViewModel(PlayerStatsManager.OverallStats stats, string filter) {
                Stats = stats;
                Filter = filter;
            }

            private AsyncCommand _rebuildOverallCommand;

            public AsyncCommand RebuildOverallCommand => _rebuildOverallCommand ?? (_rebuildOverallCommand = new AsyncCommand(async () => {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Recalculating…");
                    await PlayerStatsManager.Instance.RebuildOverallAsync();
                }
            }));

            private AsyncCommand _removeMissingCommand;

            public AsyncCommand RemoveMissingCommand => _removeMissingCommand ?? (_removeMissingCommand = new AsyncCommand(async () => {
                if (MessageDialog.Show("Are you sure you want to delete all sessions with missing content?", "Careful, please", MessageDialogButton.YesNo)
                        != MessageBoxResult.Yes) return;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Loading cars…");
                    await CarsManager.Instance.EnsureLoadedAsync();
                    waiting.Report("Loading tracks…");
                    await TracksManager.Instance.EnsureLoadedAsync();
                    waiting.Report("Clearing…");
                    var cars = CarsManager.Instance.Select(x => x.Id).ToList();
                    var tracks = TracksManager.Instance.SelectMany(x => x.MultiLayouts?.Select(y => y.IdWithLayout) ?? new[] { x.Id }).ToList();
                    await PlayerStatsManager.Instance.RebuildAndFilterAsync(s => cars.Contains(s.CarId) && tracks.Contains(s.TrackId));
                }
            }));

            private double _columns;

            public double Columns {
                get => _columns;
                set => Apply(value, ref _columns);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            Model.Columns = 4 + Math.Floor((ActualWidth - 795) / 191);
        }
    }
}