using System;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using StringBasedFilter;

namespace AcManager.Tools.Profile {
    public partial class PlayerStatsManager : NotifyPropertyChanged, IDisposable {
        private const string KeyOverall = "overall";
        private const string KeyDistancePerCarPrefix = "distancePerCar:";
        private const string KeyDistancePerTrackPrefix = "distancePerTrack:";
        private const string KeySessionStatsPrefix = "sessionStats:";

        public static int OptionRunStatsWebserver = 0;

        private static PlayerStatsManager _instance;

        public static PlayerStatsManager Instance => _instance ?? (_instance = new PlayerStatsManager());

        [CanBeNull]
        private static PlayerStatsWebServer _webServer;

        private readonly Storage _storage;

        private static OverallStats _overall;

        public event EventHandler<SessionStatsEventArgs> NewSessionAdded;

        [NotNull]
        public OverallStats Overall => _overall ?? (_overall = _storage.GetOrCreateObject<OverallStats>(KeyOverall, () => new OverallStats(_storage)));

        private WatchingSessionStats _current;
        private SessionStats _last;

        [CanBeNull]
        public SessionStats Last {
            get { return _last; }
            private set {
                if (Equals(value, _last)) return;
                _last = value;
                OnPropertyChanged();
            }
        }

        private PlayerStatsManager() {
#if DEBUG
            _storage = new Storage(FilesStorage.Instance.GetFilename("Progress", "Profile.data"), null, true);
#else
            _storage = new Storage(FilesStorage.Instance.GetFilename("Progress", "Profile.data"));
#endif
        }

        public void SetListener() {
            AcSharedMemory.Instance.Start += OnStart;
            AcSharedMemory.Instance.Finish += OnFinish;
            AcSharedMemory.Instance.Updated += OnUpdated;

            if (OptionRunStatsWebserver != 0 && _webServer == null) {
                _webServer = new PlayerStatsWebServer();
                _webServer.Start(OptionRunStatsWebserver);
            }
        }

        private void OnStart(object sender, EventArgs e) {
            if (_current != null) {
                Apply(_current);
            }

            _current = new WatchingSessionStats();
            Last = _current;
        }

        private void OnFinish(object sender, EventArgs e) {
            if (_current != null) {
                Apply(_current);
                NewSessionAdded?.Invoke(this, new SessionStatsEventArgs(_current));
                _current = null;
            }
        }

        public async Task RebuildOverall() {
            _storage.CleanUp(x => x.StartsWith(KeyDistancePerCarPrefix));
            _storage.CleanUp(x => x.StartsWith(KeyDistancePerTrackPrefix));

            var newOverall = new OverallStats(_storage);
            var delay = 0;
            foreach (var source in _storage.Where(x => x.Key.StartsWith(KeySessionStatsPrefix)).Select(x => x.Value).ToList()) {
                newOverall.Extend(SessionStats.Deserialize(source));
                if (++delay > 5) {
                    await Task.Delay(10);
                }
            }

            Overall.CopyFrom(newOverall);
            _storage.Set(KeyOverall, Overall);
        }

        private void OnUpdated(object sender, AcSharedEventArgs e) {
            _current?.Extend(e.Previous, e.Current, e.Time);
            _webServer?.PublishNewCurrentData(_current);
        }

        private void Apply(SessionStats current) {
            if (current?.CarId == null || current.TrackId == null || current.Time == TimeSpan.Zero) return;

            Logging.Debug($@"Session stats:
Penalties: {current.Penalties}
Distance: {current.DistanceKm:F1} km
Driven time: {current.Time.ToProperString()}
Max speed: {current.MaxSpeed:F1} kmh
Avg speed: {current.AverageSpeed:F1} kmh
Fuel burnt: {current.FuelBurnt:F2}l ({current.FuelConsumption:F2} liters per 100 km)
Gone offroad: {current.GoneOffroad} time(s)");

            Overall.Extend(current);
            _storage.Set(KeyOverall, Overall);
            _storage.Set(KeySessionStatsPrefix + current.StartedAt.ToMillisecondsTimestamp(), current.Serialize());
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _webServer);
        }

        private class SessionStatsTester : IParentTester<SessionStats> {
            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(SessionStats obj, string key, ITestEntry value) {
                if (key == null) {
                    if (value.Test(obj.CarId) || value.Test(obj.TrackId)) return true;

                    var track = TracksManager.Instance.GetLayoutById(obj.TrackId);
                    if (track != null && value.Test(track.Name)) return true;

                    var car = CarsManager.Instance.GetById(obj.CarId);
                    if (car != null && value.Test(car.DisplayName)) return true;
                }

                switch (key) {
                    case "d":
                    case "duration":
                    case "time":
                        return value.Test(obj.Time);

                    case "date":
                        return value.Test(obj.StartedAt);

                    case "len":
                    case "length":
                    case "distance":
                        return value.Test(obj.Distance);

                    case "speed":
                    case "average":
                    case "averagespeed":
                    case "avgspeed":
                        return value.Test(obj.MaxSpeed);

                    case "max":
                    case "maxspeed":
                    case "topspeed":
                        return value.Test(obj.MaxSpeed);
                }

                return false;
            }

            public bool TestChild(SessionStats obj, string key, IFilter filter) {
                switch (key) {
                    case null:
                    case "car":
                        var car = CarsManager.Instance.GetById(obj.CarId);
                        return car != null && filter.Test(CarObjectTester.Instance, car);

                    case "track":
                        var track = TracksManager.Instance.GetLayoutById(obj.TrackId);
                        return track != null && filter.Test(TrackBaseObjectTester.Instance, track);
                }

                return false;
            }
        }

        public OverallStats GetFiltered([NotNull] string filterValue) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));
            
            var result = new OverallStats();
            var filter = Filter.Create(new SessionStatsTester(), filterValue);
            
            foreach (var source in _storage.Where(x => x.Key.StartsWith(KeySessionStatsPrefix)).Select(x => x.Value).ToList()) {
                var stats = SessionStats.Deserialize(source);
                if (filter.Test(stats)) {
                    result.Extend(stats);
                }
            }

            return result;
        }

        public async Task<OverallStats> GetFilteredAsync(string filterValue) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));
            
            var result = new OverallStats();
            var filter = Filter.Create(new SessionStatsTester(), filterValue);

            var delay = 0;
            foreach (var source in _storage.Where(x => x.Key.StartsWith(KeySessionStatsPrefix)).Select(x => x.Value).ToList()) {
                var stats = SessionStats.Deserialize(source);
                if (filter.Test(stats)) {
                    result.Extend(stats);
                }

                if (++delay > 5) {
                    await Task.Delay(10);
                }
            }

            return result;
        }
    }
}