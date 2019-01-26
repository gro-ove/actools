using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Tools.Profile {
    public partial class PlayerStatsManager : NotifyPropertyChanged, IDisposable {
        private const string KeyOverall = "overall";
        private const string KeyDistancePerCarPrefix = "distancePerCar:";
        private const string KeyDistancePerTrackPrefix = "distancePerTrack:";
        private const string KeyMaxSpeedPerCarPrefix = "maxSpeedPerCar:";
        private const string KeySessionStatsPrefix = "sessionStats:";

        public static int OptionRunStatsWebserver = 0;
        public static string OptionWebserverFilename = null;

        public static PlayerStatsManager Instance => _instance ?? (_instance = new PlayerStatsManager());
        private static PlayerStatsManager _instance;

        [CanBeNull]
        private static RaceWebServer _webServer;

        private readonly string _storageFilename, _sessionsFilename;
        private Storage _storage;
        private List<SessionsStatsEntry> _sessions;

        private Storage GetMainStorage() {
            return _storage ?? (_storage = new Storage(_storageFilename));
        }

        private bool IsSessionsStorageReady => _sessions != null;

        private static void ParseSessions(byte[] data, int i, int l, ICollection<SessionsStatsEntry> result) {
            var count = i - l;
            if (count > 2) {
                var entry = new SessionsStatsEntry(Encoding.UTF8.GetString(data, l, count));
                result.Add(entry);
            }
        }

        private static List<SessionsStatsEntry> ParseSessions(byte[] data) {
            var result = new List<SessionsStatsEntry>(data.Length / 250);

            var l = 0;
            for (var i = 0; i < data.Length; i++) {
                if (data[i] == '\n') {
                    ParseSessions(data, i, l, result);
                    l = i + 1;
                }
            }

            ParseSessions(data, data.Length, l, result);
            result.Capacity = result.Count + 10;
            return result;
        }

        private List<SessionsStatsEntry> GetSessionsStorage() {
            if (_sessions == null) {
                if (File.Exists(_sessionsFilename)) {
                    try {
                        var data = File.ReadAllBytes(_sessionsFilename);
                        _sessions = ParseSessions(data);
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t read sessions", e);
                        _sessions = new List<SessionsStatsEntry>();
                    }
                } else {
                    _sessions = new List<SessionsStatsEntry>();
                }

                Storage.Exit += OnStorageExit;
            }

            return _sessions;
        }

        private async Task<List<SessionsStatsEntry>> GetSessionsStorageAsync() {
            if (_sessions == null) {
                if (File.Exists(_sessionsFilename)) {
                    try {
                        var data = await FileUtils.ReadAllBytesAsync(_sessionsFilename);
                        _sessions = await Task.Run(() => ParseSessions(data));
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t read sessions", e);
                        _sessions = new List<SessionsStatsEntry>();
                    }
                } else {
                    _sessions = new List<SessionsStatsEntry>();
                }
            }

            return _sessions;
        }

        public void SaveSessions() {
            using (var writer = File.CreateText(_sessionsFilename)) {
                foreach (var entry in _sessions) {
                    writer.WriteLine(entry.Serialized);
                }
            }
        }

        public async Task SaveSessionsAsync() {
            using (var writer = File.CreateText(_sessionsFilename)) {
                foreach (var entry in _sessions) {
                    await writer.WriteLineAsync(entry.Serialized.TrimEnd());
                }
            }
        }

        private bool _dirty;

        private async void SessionsDirty() {
            if (_dirty) return;

            try {
                _dirty = true;
                await Task.Delay(5000);
                if (_dirty) {
                    await SaveSessionsAsync();
                }
            } finally {
                _dirty = false;
            }
        }

        private void OnStorageExit(object sender, EventArgs e) {
            if (_dirty) {
                SaveSessions();
                _dirty = false;
            }
        }

        private class SessionsStatsEntry {
            [NotNull]
            private readonly string _serialized;

            public string Serialized => _serialized;

            [CanBeNull]
            private SessionStats _parsedData;

            private bool _parsed;

            [CanBeNull]
            public SessionStats Parsed {
                get {
                    if (!_parsed) {
                        _parsed = true;
                        _parsedData = SessionStats.TryToDeserialize(_serialized);
                    }

                    return _parsedData;
                }
            }

            public SessionsStatsEntry([NotNull] string serialized) {
                _serialized = serialized;
            }

            public SessionsStatsEntry([NotNull] SessionStats data) {
                _parsedData = data;
                _serialized = data.Serialize();
            }
        }

        public event EventHandler<SessionStatsEventArgs> NewSessionAdded;

        private static OverallStats _overall;

        [NotNull]
        public OverallStats Overall {
            get {
                if (_overall == null) {
                    var storage = GetMainStorage();
                    _overall = storage.GetOrCreateObject<OverallStats>(KeyOverall);
                    _overall.Storage = storage;
                }

                return _overall;
            }
        }

        // private readonly Dictionary<string, OverallStats> _prepared = new Dictionary<string, OverallStats>();

        private WatchingSessionStats _current;
        private SessionStats _last;

        [CanBeNull]
        public SessionStats Last {
            get => _last;
            private set => Apply(value, ref _last);
        }

        private PlayerStatsManager() {
            _storageFilename = FilesStorage.Instance.GetFilename("Progress", "Profile.data");
            _sessionsFilename = FilesStorage.Instance.GetFilename("Progress", "Profile (Sessions).data");

            if (File.Exists(_storageFilename) && !File.Exists(_sessionsFilename)) {
                Logging.Debug("Converting stats to new format…");

                using (var writer = File.AppendText(_sessionsFilename)) {
                    var storage = GetMainStorage();
                    foreach (var source in storage.Where(x => x.Key.StartsWith(KeySessionStatsPrefix)).ToList()) {
                        writer.WriteLine(source.Value);
                        storage.Remove(source.Key);
                    }
                }
            }
        }

        private bool _setListener;

        public void SetListener() {
            if (_setListener) return;
            _setListener = true;

            GameWrapper.Started += OnSessionStarted;
            AcSharedMemory.Instance.Start += OnStart;
            AcSharedMemory.Instance.Finish += OnFinish;
            AcSharedMemory.Instance.Updated += OnUpdated;

            if (OptionRunStatsWebserver != 0 && _webServer == null) {
                _webServer = new RaceWebServer();
                _webServer.Start(OptionRunStatsWebserver, OptionWebserverFilename);
            }
        }

        private void OnSessionStarted(object sender, GameStartedArgs gameStartedArgs) {
            Last = null;
        }

        private void OnStart(object sender, EventArgs e) {
            // Logging.Here();

            if (_current != null) {
                Apply(_current);
            }

            _current = new WatchingSessionStats();
            Last = _current;

            if (CarCustomDataHelper.IsActive) {
                _current.SetSpoiled("Custom car data");
            }
        }

        private void OnFinish(object sender, EventArgs e) {
            // Logging.Here();

            var current = _current;
            if (current != null) {
                _current = null;

                if (current.MaxSpeed > 800d || current.AverageSpeed > 500d || current.Distance > 10e6) {
                    current.SetSpoiled("Potentially damaged values");
                }

                if (current.IsSpoiled) {
                    Logging.Warning("Session spoiled: " + current.SpoiledReason);
                } else {
                    Apply(current);
                    NewSessionAdded?.Invoke(this, new SessionStatsEventArgs(current));
                }
            }
        }

        public async Task RebuildAndFilterAsync(Func<SessionStats, bool> filter) {
            var newStorage = new Storage();
            var newOverall = new OverallStats(newStorage);
            var newSessions = new List<SessionsStatsEntry>();

            var sessions = (await GetSessionsStorageAsync()).ToList();
            await Task.Run(() => {
                for (var i = 0; i < sessions.Count; i++) {
                    var session = sessions[i].Parsed;
                    if (session != null && filter?.Invoke(session) != false) {
                        newOverall.Extend(session);
                        newSessions.Add(sessions[i]);
                    }
                }
            });

            Overall.CopyFrom(newOverall);
            newStorage.SetObject(KeyOverall, Overall);
            GetMainStorage().CopyFrom(newStorage);
            _sessions = newSessions;
            await SaveSessionsAsync();

            if (SettingsHolder.Drive.StereoOdometerImportValues) {
                StereoOdometerHelper.ImportAll();
            }
        }

        public Task RebuildOverallAsync() {
            return RebuildAndFilterAsync(null);
        }

        public Task RemoveCarAsync(params string[] ids) {
            return RebuildAndFilterAsync(s => !ids.Contains(s.CarId));
        }

        public Task RemoveTrackAsync(params string[] ids) {
            return RebuildAndFilterAsync(s => ids.All(y => s.TrackId?.Split('/')[0] != y));
        }

        public Task RemoveTrackLayoutAsync(params string[] idsWithLayout) {
            return RebuildAndFilterAsync(s => !idsWithLayout.Contains(s.TrackId));
        }

        private void OnUpdated(object sender, AcSharedEventArgs e) {
            // Logging.Here();

            _current?.Extend(e.Previous, e.Current, e.Time);
            if (_webServer != null) {
                _webServer.PublishStatsData(_current);
                _webServer.PublishSharedData(e.Current);
            }
        }

        private void ExtendOveralls(SessionStats current) {
            var storage = GetMainStorage();
            Overall.Extend(current);
            storage.SetObject(KeyOverall, Overall);

            foreach (var holded in _holdedList) {
                holded.Extend(current);
            }
        }

        private void AppendSession(SessionStats current) {
            if (IsSessionsStorageReady) {
                GetSessionsStorage().Add(new SessionsStatsEntry(current));
            }

            using (var writer = File.AppendText(_sessionsFilename)) {
                writer.WriteLine(current.Serialize());
            }
        }

        private static void UpdateCarDrivenDistanceAndMaxSpeed(SessionStats current) {
            var car = current.CarId == null ? null : CarsManager.Instance.GetWrapperById(current.CarId)?.Value as CarObject;
            if (car == null) return;

            car.RaiseMaxSpeedAchievedChanged();
            car.RaiseTotalDrivenDistanceChanged();
        }

        private static void UpdateTrackDrivenDistance(SessionStats current) {
            if (current.TrackId == null) return;
            (TracksManager.Instance.GetWrappedByIdWithLayout(current.TrackId)?.Value as TrackObject)?
                    .GetLayoutById(current.TrackId)?.RaiseTotalDrivenDistanceChanged();
        }

        private void Apply(SessionStats current) {
            if (current?.CarId == null || current.TrackId == null || current.Time == TimeSpan.Zero) return;

            Logging.Debug($@"Session stats:
Penalties: {current.Penalties}
Distance: {current.DistanceKm:F1} km
Driven time: {current.Time.ToProperString()}
Max speed: {current.MaxSpeed:F1} km/h
Avg speed: {current.AverageSpeed:F1} km/h
Fuel burnt: {current.FuelBurnt:F2}l ({current.FuelConsumption:F2} liters per 100 km)
Gone offroad: {current.GoneOffroad} time(s)");

            ExtendOveralls(current);
            AppendSession(current);
            UpdateCarDrivenDistanceAndMaxSpeed(current);
            UpdateTrackDrivenDistance(current);
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _webServer);
        }

        private class SessionStatsTester : IParentTester<SessionStats> {
            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(SessionStats obj, string key, ITestEntry value) {
                switch (key) {
                    case null:
                        if (value.Test(obj.CarId) || value.Test(obj.TrackId)) return true;

                        var track = obj.TrackId == null ? null : TracksManager.Instance.GetLayoutById(obj.TrackId);
                        if (track != null && value.Test(track.Name)) return true;

                        var car = obj.CarId == null ? null : CarsManager.Instance.GetById(obj.CarId);
                        if (car != null && value.Test(car.DisplayName)) return true;

                        return false;

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

                    default:
                        return false;
                }
            }

            public bool TestChild(SessionStats obj, string key, IFilter filter) {
                switch (key) {
                    case null:
                    case "car":
                        var car = obj.CarId == null ? null : CarsManager.Instance.GetById(obj.CarId);
                        return car != null && filter.Test(CarObjectTester.Instance, car);

                    case "track":
                        var track = obj.TrackId == null ? null : TracksManager.Instance.GetLayoutById(obj.TrackId);
                        return track != null && filter.Test(TrackObjectBaseTester.Instance, track);
                }

                return false;
            }
        }

        private readonly HoldedList<OverallStats> _holdedList = new HoldedList<OverallStats>(4);

        public Holder<OverallStats> GetFiltered([NotNull] string filterValue) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));

            var result = new FilteredStats(filterValue);
            foreach (var source in GetSessionsStorage()) {
                var stats = source.Parsed;
                if (stats != null) {
                    result.Extend(stats);
                }
            }

            return _holdedList.Get(result);
        }

        public async Task<Holder<OverallStats>> GetFilteredAsync(string filterValue) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));

            var storage = (await GetSessionsStorageAsync()).ToList();
            return _holdedList.Get(await Task.Run(() => {
                var result = new FilteredStats(filterValue);
                for (var i = 0; i < storage.Count; i++) {
                    var stats = storage[i].Parsed;
                    if (stats != null) {
                        result.Extend(stats);
                    }
                }

                return result;
            }));
        }

        public IEnumerable<string> GetCarsIds() {
            return GetMainStorage().Where(x => x.Key.StartsWith(KeyDistancePerCarPrefix))
                                   .Select(x => x.Key.Substring(KeyDistancePerCarPrefix.Length));
        }

        /// <summary>
        /// Meters!
        /// </summary>
        public double GetDistanceDrivenByCar(string carId) {
            return GetMainStorage().Get(KeyDistancePerCarPrefix + carId, 0d);
        }

        /// <summary>
        /// Meters!
        /// </summary>
        public void SetDistanceDrivenByCar(string carId, double value) {
            GetMainStorage().Set(KeyDistancePerCarPrefix + carId, value);
        }

        public double GetMaxSpeedByCar(string carId) {
            return GetMainStorage().Get(KeyMaxSpeedPerCarPrefix + carId, 0d);
        }

        public IEnumerable<string> GetTrackLayoutsIds() {
            return GetMainStorage().Where(x => x.Key.StartsWith(KeyDistancePerTrackPrefix))
                                   .Select(x => x.Key.Substring(KeyDistancePerTrackPrefix.Length));
        }

        public double GetDistanceDrivenAtTrack(string trackLayoutId) {
            return GetMainStorage().Get(KeyDistancePerTrackPrefix + trackLayoutId, 0d);
        }

        private static IEnumerable<string> FixAcTrackId(string acTrackId) {
            yield return acTrackId;

            var i = 0;
            while (true) {
                i = acTrackId.IndexOf('-', i + 1);
                if (i == -1 || i == acTrackId.Length - 1) yield break;
                yield return acTrackId.Substring(0, i) + '/' + acTrackId.Substring(i + 1);
            }
        }

        public double GetDistanceDrivenAtTrackAcId(string trackLayoutId) {
            var storage = GetMainStorage();
            foreach (var f in FixAcTrackId(trackLayoutId)) {
                var k = KeyDistancePerTrackPrefix + f;
                if (storage.Contains(k)) {
                    return storage.Get(k, 0d);
                }
            }

            return 0d;
        }
    }
}