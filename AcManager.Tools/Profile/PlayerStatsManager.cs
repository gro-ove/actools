using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
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
        private const string KeySessionStatsPrefix = "sessionStats:";

        public static int OptionRunStatsWebserver = 0;
        public static string OptionWebserverFilename = null;

        public static PlayerStatsManager Instance => _instance ?? (_instance = new PlayerStatsManager());
        private static PlayerStatsManager _instance;

        [CanBeNull]
        private static RaceWebServer _webServer;

        private readonly string _storageFilename, _sessionsFilename;
        private Storage _storage3;
        private List<SessionsStatsEntry> _sessions3;

        private Storage GetMainStorage() {
            return _storage3 ?? (_storage3 = new Storage(_storageFilename));
        }

        private bool IsSessionsStorageReady => _sessions3 != null;

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
            if (_sessions3 == null) {
                if (File.Exists(_sessionsFilename)) {
                    try {
                        var data = File.ReadAllBytes(_sessionsFilename);
                        _sessions3 = ParseSessions(data);
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t read sessions", e);
                        _sessions3 = new List<SessionsStatsEntry>();
                    }
                } else {
                    _sessions3 = new List<SessionsStatsEntry>();
                }

                Storage.Exit += OnStorageExit;
            }

            return _sessions3;
        }

        private async Task<List<SessionsStatsEntry>> GetSessionsStorageAsync() {
            if (_sessions3 == null) {
                if (File.Exists(_sessionsFilename)) {
                    try {
                        var data = await FileUtils.ReadAllBytesAsync(_sessionsFilename);
                        _sessions3 = await Task.Run(() => ParseSessions(data));
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t read sessions", e);
                        _sessions3 = new List<SessionsStatsEntry>();
                    }
                } else {
                    _sessions3 = new List<SessionsStatsEntry>();
                }
            }

            return _sessions3;
        }

        public void SaveSessions() {
            using (var writer = File.CreateText(_sessionsFilename)) {
                foreach (var entry in _sessions3) {
                    writer.WriteLine(entry.Serialized);
                }
            }
        }

        public async Task SaveSessionsAsync() {
            using (var writer = File.CreateText(_sessionsFilename)) {
                foreach (var entry in _sessions3) {
                    await writer.WriteLineAsync(entry.Serialized);
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
                        _parsedData = SessionStats.TryDeserialize(_serialized);
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
            _storageFilename = FilesStorage.Instance.GetFilename("Progress", "Profile.data");
            _sessionsFilename = FilesStorage.Instance.GetFilename("Progress", "Profile (Sessions).data");

            if (File.Exists(_storageFilename) && !File.Exists(_sessionsFilename)) {
                var storage = GetMainStorage();

                Logging.Debug("Converting stats to new format…");

                using (var writer = File.AppendText(_sessionsFilename)) {
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

            AcSharedMemory.Instance.Start += OnStart;
            AcSharedMemory.Instance.Finish += OnFinish;
            AcSharedMemory.Instance.Updated += OnUpdated;

            if (OptionRunStatsWebserver != 0 && _webServer == null) {
                _webServer = new RaceWebServer();
                _webServer.Start(OptionRunStatsWebserver, OptionWebserverFilename);
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
            var current = _current;
            if (current != null) {
                _current = null;
                Apply(current);
                NewSessionAdded?.Invoke(this, new SessionStatsEventArgs(current));
            }
        }

        public async Task RebuildOverall() {
            var newStorage = new Storage();
            var newOverall = new OverallStats(newStorage);

            var sessions = (await GetSessionsStorageAsync()).ToList();
            await Task.Run(() => {
                for (var i = 0; i < sessions.Count; i++) {
                    newOverall.Extend(sessions[i].Parsed);
                }
            });

            Overall.CopyFrom(newOverall);
            newStorage.SetObject(KeyOverall, Overall);

            var storage = GetMainStorage();
            storage.CopyFrom(newStorage);
        }

        private void OnUpdated(object sender, AcSharedEventArgs e) {
            _current?.Extend(e.Previous, e.Current, e.Time);
            if (_webServer != null) {
                _webServer.PublishStatsData(_current);
                _webServer.PublishSharedData(e.Current);
            }
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

            var storage = GetMainStorage();
            Overall.Extend(current);
            storage.SetObject(KeyOverall, Overall);
            
            if (IsSessionsStorageReady) {
                GetSessionsStorage().Add(new SessionsStatsEntry(current));
            }

            using (var writer = File.AppendText(_sessionsFilename)) {
                writer.WriteLine(current.Serialize());
            }
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

        public static bool FilterTest([NotNull] string filterValue, [NotNull] SessionStats stats) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));
            return Filter.Create(new SessionStatsTester(), filterValue).Test(stats);
        }

        public OverallStats GetFiltered([NotNull] string filterValue) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));
            
            var result = new OverallStats();
            var filter = Filter.Create(new SessionStatsTester(), filterValue);

            foreach (var source in GetSessionsStorage()) {
                var stats = source.Parsed;
                if (stats != null && filter.Test(stats)) {
                    result.Extend(stats);
                }
            }

            return result;
        }

        public async Task<OverallStats> GetFilteredAsync(string filterValue) {
            if (filterValue == null) throw new ArgumentNullException(nameof(filterValue));
            
            var storage = (await GetSessionsStorageAsync()).ToList();
            return await Task.Run(() => {
                var result = new OverallStats();
                var filter = Filter.Create(new SessionStatsTester(), filterValue);

                for (var i = 0; i < storage.Count; i++) {
                    var stats = storage[i].Parsed;
                    if (stats != null && filter.Test(stats)) {
                        result.Extend(stats);
                    }
                }

                return result;
            });
        }
    }
}