using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Miscellaneous {
    public partial class MostUsed : IParametrizedUriContent, ILoadableContent {
        private IFilter<AcObjectNew> _filter;
        private List<MostUsedCar> _cars;
        private List<MostUsedTrack> _tracks;

        private ViewModel Model => (ViewModel)DataContext;

        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            _filter = filter == null ? null : Filter.Create(UniversalAcObjectTester.Instance, filter);
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_filter == null) {
                _cars = GetCars().ToList();
                _tracks = GetTracks().ToList();
            } else {
                _cars = new List<MostUsedCar>();
                foreach (var most in GetCars()) {
                    var car = await CarsManager.Instance.GetByIdAsync(most.AcObjectId);
                    if (car != null && _filter.Test(car)) {
                        _cars.Add(most);
                    }
                }

                _tracks = new List<MostUsedTrack>();
                foreach (var most in GetTracks()) {
                    var track = (await TracksManager.Instance.GetByIdAsync(most.AcObjectId))?.GetLayoutById(most.AcObjectId);
                    if (track != null && _filter.Test(track)) {
                        _tracks.Add(most);
                    }
                }
            }
        }

        public void Load() {
            if (_filter == null) {
                _cars = GetCars().ToList();
                _tracks = GetTracks().ToList();
            } else {
                _cars = (from most in GetCars()
                         where most.Car != null && _filter.Test(most.Car)
                         select most).ToList();
                _tracks = (from most in GetTracks()
                           where most.Track != null && _filter.Test(most.Track)
                           select most).ToList();
            }
        }

        public void Initialize() {
            DataContext = new ViewModel(_cars, _tracks);
            InitializeComponent();
        }

        private static IEnumerable<MostUsedCar> GetCars() {
            var index = 0;
            return from carId in PlayerStatsManager.Instance.GetCarsIds()
                   let distance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId)
                   where distance > 0d
                   orderby distance descending
                   select new MostUsedCar(index++, carId, distance / 1e3);
        }

        private static IEnumerable<MostUsedTrack> GetTracks() {
            var index = 0;
            return from trackId in PlayerStatsManager.Instance.GetTrackLayoutsIds()
                   let distance = PlayerStatsManager.Instance.GetDistanceDrivenAtTrack(trackId)
                   where distance > 0d
                   orderby distance descending
                   select new MostUsedTrack(index++, trackId, distance / 1e3);
        }

        public class MostUsedObject {
            public MostUsedObject(int index, [NotNull] string acObjectId, double totalDistance) {
                Position = index + 1;
                AcObjectId = acObjectId;
                TotalDistance = totalDistance;
            }

            public int Position { get; }

            [NotNull]
            public string AcObjectId { get; }

            public double TotalDistance { get; }
        }

        public class MostUsedCar : MostUsedObject {
            public MostUsedCar(int index, [NotNull] string acObjectId, double totalDistance) : base(index, acObjectId, totalDistance) { }

            private bool _set;
            private CarObject _car;

            [CanBeNull]
            public CarObject Car {
                get {
                    if (!_set) {
                        _set = true;
                        _car = CarsManager.Instance.GetById(AcObjectId);
                    }
                    return _car;
                }
            }
        }

        public class MostUsedTrack : MostUsedObject {
            public MostUsedTrack(int index, [NotNull] string acObjectId, double totalDistance) : base(index, acObjectId, totalDistance) { }

            private bool _set;
            private TrackObjectBase _track;

            [CanBeNull]
            public TrackObjectBase Track {
                get {
                    if (!_set) {
                        _set = true;
                        _track = TracksManager.Instance.GetLayoutByShortenId(AcObjectId);
                    }
                    return _track;
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public BetterObservableCollection<MostUsedCar> CarEntries { get; }

            public BetterObservableCollection<MostUsedTrack> TrackEntries { get; }

            public ViewModel(List<MostUsedCar> cars, List<MostUsedTrack> tracks) {
                CarEntries = new BetterObservableCollection<MostUsedCar>(cars);
                TrackEntries = new BetterObservableCollection<MostUsedTrack>(tracks);
            }
        }
    }
}
