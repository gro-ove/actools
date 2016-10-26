using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Managers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Miscellaneous {
    /// <summary>
    /// Interaction logic for LapTimes.xaml
    /// </summary>
    public partial class PlayerStats : ILoadableContent, IParametrizedUriContent {
        private string _filter;
        private PlayerStatsManager.OverallStats _stats;

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

            _stats = _filter == null ? PlayerStatsManager.Instance.Overall : await PlayerStatsManager.Instance.GetFilteredAsync(_filter);
        }

        public void Load() {
            _stats = _filter == null ? PlayerStatsManager.Instance.Overall : PlayerStatsManager.Instance.GetFiltered(_filter);
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new ViewModel(_stats);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public PlayerStatsManager.OverallStats Stats { get; }

            public ViewModel(PlayerStatsManager.OverallStats stats) {
                Stats = stats;
            }

            private AsyncCommand _rebuildOverallCommand;

            public AsyncCommand RebuildOverallCommand => _rebuildOverallCommand ?? (_rebuildOverallCommand = new AsyncCommand(async () => {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Rebuilding…");
                    await PlayerStatsManager.Instance.RebuildOverall();
                }
            }));

            private double _columns;

            public double Columns {
                get { return _columns; }
                set {
                    if (Equals(value, _columns)) return;
                    _columns = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            PlayerStatsManager.Instance.NewSessionAdded += NewSessionAdded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            PlayerStatsManager.Instance.NewSessionAdded -= NewSessionAdded;
        }

        private void NewSessionAdded(object sender, PlayerStatsManager.SessionStatsEventArgs e) {
            if (_filter != null) {
                Model.Stats.Extend(e.Stats);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            Model.Columns = 4 + Math.Floor((ActualWidth - 795) / 191);
        }
    }
}
