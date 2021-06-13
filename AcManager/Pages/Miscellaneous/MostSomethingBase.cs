using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Pages.Windows;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
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
    public abstract class MostSomethingBase : UserControl, IParametrizedUriContent, ILoadableContent {
        private string _filter;
        private IStorage _storage;
        private List<MostUsedCar> _cars;
        private List<MostUsedTrack> _tracks;

        public void OnUri(Uri uri) {
            _filter = uri.GetQueryParam("Filter");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_filter != null) {
                var stats = await PlayerStatsManager.Instance.GetFilteredAsync(_filter);
                _storage = stats.Value.Storage;
                this.OnActualUnload(stats);
            }
            _cars = GetCars().ToList();
            _tracks = GetTracks().ToList();
        }

        public void Load() {
            if (_filter != null) {
                var stats = PlayerStatsManager.Instance.GetFiltered(_filter);
                _storage = stats.Value.Storage;
                this.OnActualUnload(stats);
            }
            _cars = GetCars().ToList();
            _tracks = GetTracks().ToList();
        }

        public void Initialize() {
            Initialize(new ViewModel(_cars, _tracks));
        }

        protected abstract void Initialize(ViewModel viewModel);
        protected abstract double GetTrackValue(string trackId, IStorage storage);
        protected abstract double GetCarValue(string carId, IStorage storage);
        protected abstract string GetDisplayValue(double value);
        protected abstract double CalculateTotalTrackValue(IEnumerable<double> value);

        private IEnumerable<MostUsedCar> GetCars() {
            var index = 0;
            return from carId in PlayerStatsManager.Instance.GetCarsIds(_storage)
                let value = GetCarValue(carId, _storage)
                where value > 0d && CarsManager.Instance.GetWrapperById(carId) != null
                orderby value descending
                select new MostUsedCar(index++, carId, value, GetDisplayValue(value));
        }

        private IEnumerable<MostUsedTrack> GetTracks() {
            var index = 0;
            return from trackId in PlayerStatsManager.Instance.GetTrackLayoutsIds(_storage)
                let track = new {
                    Id = trackId,
                    MainId = GetMainTrackId(trackId),
                    Value = GetTrackValue(trackId, _storage)
                }
                where track.Value > 0d && TracksManager.Instance.GetWrapperById(track.MainId) != null
                group track by track.MainId
                into layouts
                orderby layouts.Sum(x => x.Value) descending
                let totalValue = CalculateTotalTrackValue(layouts.Select(x => x.Value))
                let item = new MostUsedTrack(index++, layouts.Key, totalValue, GetDisplayValue(totalValue),
                        layouts.Select((x, i) => new MostUsedTrackLayout(i, x.Id, x.Value, GetDisplayValue(x.Value))))
                select item;

            string GetMainTrackId(string trackWithLayoutId) {
                var separator = trackWithLayoutId.IndexOf('/');
                return separator < 0 ? trackWithLayoutId : trackWithLayoutId.Substring(0, separator);
            }
        }

        public class MostUsedObject : NotifyPropertyChanged {
            public MostUsedObject(int index, [NotNull] string acObjectId, double totalValue, string displayValue) {
                Position = index + 1;
                AcObjectId = acObjectId;
                TotalValue = totalValue;
                DisplayTotalValue = displayValue;
            }

            public int Position { get; }

            [NotNull]
            public string AcObjectId { get; }

            public double TotalValue { get; }

            public string DisplayTotalValue { get; }
        }

        public class MostUsedCar : MostUsedObject {
            public MostUsedCar(int index, [NotNull] string acObjectId, double totalValue, string displayValue)
                    : base(index, acObjectId, totalValue, displayValue) {
                Car = Lazier.CreateAsync(() => CarsManager.Instance.GetByIdAsync(AcObjectId));
            }

            public Lazier<CarObject> Car { get; }
        }

        public class MostUsedTrack : MostUsedObject {
            public List<MostUsedTrackLayout> Layouts { get; }

            public MostUsedTrack(int index, [NotNull] string acObjectId, double totalValue, string displayValue, IEnumerable<MostUsedTrackLayout> layouts)
                    : base(index, acObjectId, totalValue, displayValue) {
                Track = Lazier.CreateAsync(() => TracksManager.Instance.GetByIdAsync(AcObjectId));
                Layouts = layouts.ToList();
                if (Layouts.Count == 1) {
                    Layouts.Clear();
                }
            }

            public Lazier<TrackObject> Track { get; }

            private bool _isExpanded;

            public bool IsExpanded {
                get => _isExpanded;
                set => Apply(value, ref _isExpanded);
            }
        }

        public class MostUsedTrackLayout : MostUsedObject {
            public MostUsedTrackLayout(int index, [NotNull] string acObjectId, double totalValue, string displayValue)
                    : base(index, acObjectId, totalValue, displayValue) {
                Track = Lazier.CreateAsync(() => TracksManager.Instance.GetLayoutByShortenIdAsync(AcObjectId));
            }

            public Lazier<TrackObjectBase> Track { get; }
        }

        public class ViewModel : NotifyPropertyChanged {
            public BetterObservableCollection<MostUsedCar> CarEntries { get; }

            public BetterObservableCollection<MostUsedTrack> TrackEntries { get; }

            public ViewModel(List<MostUsedCar> cars, List<MostUsedTrack> tracks) {
                CarEntries = new BetterObservableCollection<MostUsedCar>(cars);
                TrackEntries = new BetterObservableCollection<MostUsedTrack>(tracks);
            }

            private AsyncCommand _rebuildOverallCommand;

            public AsyncCommand RebuildOverallCommand => _rebuildOverallCommand ?? (_rebuildOverallCommand = new AsyncCommand(async () => {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Recalculating…");
                    await PlayerStatsManager.Instance.RebuildOverallAsync();
                    if (Application.Current?.MainWindow is MainWindow window) {
                        window.NavigateTo(window.CurrentSource);
                    }
                }
            }));
        }
    }
}