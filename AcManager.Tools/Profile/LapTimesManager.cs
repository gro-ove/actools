using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.LapTimes;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    public class LapTimesManager : NotifyPropertyChanged, IAcIdsProvider {
        public static readonly string SourceId = @"Content Manager";

        private static LapTimesManager _instance;

        public static LapTimesManager Instance => _instance ?? (_instance = new LapTimesManager());

        private LapTimesStorage _cmStorage;
        private LapTimesStorage _acDbStorage;
        private LapTimesStorage _acStorageNew;
        private LapTimesStorage _sidekickStorage;
        private LapTimesStorage _raceEssentialsStorage;

        private LapTimesManager() {
            Entries = new BetterObservableCollection<LapTimeEntry>();
            SettingsHolder.LapTimes.PropertyChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.LapTimes.SourceAcDb):
                    DisposeHelper.Dispose(ref _acDbStorage);
                    break;
                case nameof(SettingsHolder.LapTimes.SourceAcNew):
                    DisposeHelper.Dispose(ref _acStorageNew);
                    break;
                case nameof(SettingsHolder.LapTimes.SourceSidekick):
                    DisposeHelper.Dispose(ref _sidekickStorage);
                    break;
                case nameof(SettingsHolder.LapTimes.SourceRaceEssentials):
                    DisposeHelper.Dispose(ref _raceEssentialsStorage);
                    break;
            }

            _lastUpdate = default(DateTime);
        }

        public void SetListener() {
            GameWrapper.Ended += OnRaceFinished;
        }

        public event EventHandler NewEntryAdded;

        private void OnRaceFinished(object sender, GameEndedArgs e) {
            var basic = e.StartProperties.BasicProperties;
            var bestLap = e.Result?.GetExtraByType<Game.ResultExtraBestLap>();

            var carId = basic?.CarId;
            var trackId = basic?.TrackId;

            if (trackId != null && basic.TrackConfigurationId != null) {
                trackId = $@"{trackId}/{basic.TrackConfigurationId}";
            }

            var time = carId == null || trackId == null || bestLap == null ||
                    bestLap.IsCancelled ? (TimeSpan?)null : bestLap.Time;

            if (SettingsHolder.Drive.WatchForSharedMemory) {
                var last = PlayerStatsManager.Instance.Last;

                if (last != null) {
                    if (carId == null) {
                        carId = last.CarId;
                    }

                    if (trackId == null) {
                        trackId = last.TrackId;
                    }

                    var sharedTime = last.BestLap;
                    if (sharedTime.HasValue) {
                        time = sharedTime.Value;
                    }
                }
            }

            if (time.HasValue && carId != null && trackId != null) {
                Logging.Here();
                AddEntry(new LapTimeEntry(SourceId, carId, trackId, DateTime.Now, time.Value));
                NewEntryAdded?.Invoke(this, EventArgs.Empty);
            } else {
                Logging.Warning($"Can’t save new lap time: time={time}, car={carId}, track={trackId}");
            }
        }

        [CanBeNull]
        private LapTimeEntry Get(string carId, string trackId) {
            trackId = trackId.Replace('/', '-');
            return Entries.FirstOrDefault(x => string.Equals(x.CarId, carId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.TrackAcId, trackId, StringComparison.OrdinalIgnoreCase));
        }

        private void AddEntry(LapTimeEntry entry) {
            InitializeCm();
            if (_cmStorage.IsBetter(entry)) {
                _cmStorage.Set(entry);
                UpdateAsync().Forget();
            }
        }

        private void InitializeCm() {
            if (_cmStorage != null) return;
            _cmStorage = new LapTimesStorage(SourceId);
        }

        private void InitializeAc() {
            if (_acDbStorage != null) return;
            _acDbStorage = new LapTimesStorage(AcLapTimesReader.SourceId);
        }

        private void InitializeAcNew() {
            if (_acStorageNew != null) return;
            _acStorageNew = new LapTimesStorage(AcLapTimesNewReader.SourceId);
        }

        private void InitializeSidekick() {
            if (_sidekickStorage != null) return;
            _sidekickStorage = new LapTimesStorage(SidekickLapTimesReader.SourceId);
        }

        private void InitializeRaceEssentials() {
            if (_raceEssentialsStorage != null) return;
            _raceEssentialsStorage = new LapTimesStorage(RaceEssentialsLapTimesReader.SourceId);
        }

        public BetterObservableCollection<LapTimeEntry> Entries { get; }

        private DateTime _lastUpdate;

        private bool IsActual() {
            if ((DateTime.Now - _lastUpdate).TotalMinutes < 1) return true;

            _lastUpdate = DateTime.Now;
            return false;
        }

        private IList<LapTimeEntry> KeepBetterOnes(IEnumerable<LapTimeEntry> entries) {
            var result = new List<LapTimeEntry>();
            foreach (var entry in entries) {
                var existingIndex = result.FindIndex(x => x.Same(entry));
                if (existingIndex != -1) {
                    var existing = result[existingIndex];
                    if (existing.LapTime > entry.LapTime) {
                        result.RemoveAt(existingIndex);
                    } else {
                        continue;
                    }
                }

                result.Add(entry);
            }

            return result;
        }

        public async Task UpdateAsync() {
            if (IsActual()) return;

            var cmEntries = SettingsHolder.LapTimes.SourceCm ? ReadCmEntries() : new LapTimeEntry[0];
            var acDbEntries = SettingsHolder.LapTimes.SourceAcDb ? await ReadAcDbEntriesAsync() : new LapTimeEntry[0];
            var acNewEntries = SettingsHolder.LapTimes.SourceAcNew ? await ReadAcNewEntriesAsync() : new LapTimeEntry[0];
            var sidekickEntries = SettingsHolder.LapTimes.SourceSidekick ? await ReadSidekickEntriesAsync() : new LapTimeEntry[0];
            var raceEssectialsEntries = SettingsHolder.LapTimes.SourceRaceEssentials ? await ReadRaceEssentialsEntriesAsync() : new LapTimeEntry[0];
            Entries.ReplaceEverythingBy(KeepBetterOnes(
                    cmEntries.Concat(acDbEntries)
                             .Concat(acNewEntries)
                             .Concat(sidekickEntries)
                             .Concat(raceEssectialsEntries)
                             .OrderByDescending(x => x.EntryDate)));
        }
        
        public async Task ExportAsync(string key) {
            ILapTimesReader reader;
            if (key == SourceId) {
                InitializeCm();
                await Task.Run(() => {
                    foreach (var entry in Entries.ToList()) {
                        _cmStorage.Set(entry);
                    }
                });
                return;
            } else if (key == AcLapTimesNewReader.SourceId) {
                reader = new AcLapTimesNewReader(FileUtils.GetDocumentsDirectory(), this);
            } else if (key == SidekickLapTimesReader.SourceId) {
                var sidekickDirectory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "Sidekick");
                reader = new SidekickLapTimesReader(sidekickDirectory, this);
            } else if (key == RaceEssentialsLapTimesReader.SourceId) {
                var sidekickDirectory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "RaceEssentials");
                reader = new RaceEssentialsLapTimesReader(sidekickDirectory, this);
            } else {
                throw new NotSupportedException();
            }

            await Task.Run(() => reader.Export(Entries));
        }

        private IEnumerable<LapTimeEntry> ReadCmEntries() {
            InitializeCm();
            return _cmStorage.GetLapTimes();
        }

        private async Task<IReadOnlyList<LapTimeEntry>> ReadAcDbEntriesAsync() {
            InitializeAc();
            return await Task.Run(() => {
                using (var reader = new AcLapTimesReader(FileUtils.GetDocumentsDirectory())) {
                    return _acDbStorage.GetLapTimesList(reader);
                }
            });
        }

        private async Task<IReadOnlyList<LapTimeEntry>> ReadAcNewEntriesAsync() {
            await TracksManager.Instance.EnsureLoadedAsync();

            InitializeAcNew();
            return await Task.Run(() => {
                using (var reader = new AcLapTimesNewReader(FileUtils.GetDocumentsDirectory(), this)) {
                    return _acStorageNew.GetLapTimesList(reader);
                }
            });
        }

        private async Task<IReadOnlyList<LapTimeEntry>> ReadSidekickEntriesAsync() {
            await TracksManager.Instance.EnsureLoadedAsync();

            InitializeSidekick();
            var sidekickDirectory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "Sidekick");
            using (var reader = new SidekickLapTimesReader(sidekickDirectory, this)) {
                var result = _sidekickStorage.GetCachedLapTimesList(reader);
                if (result != null) return result;

                await TracksManager.Instance.EnsureLoadedAsync();
                return _sidekickStorage.UpdateCachedLapTimesList(reader);
            }
        }

        private async Task<IReadOnlyList<LapTimeEntry>> ReadRaceEssentialsEntriesAsync() {
            InitializeRaceEssentials();
            var raceEssentialsDirectory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "RaceEssentials");
            using (var reader = new RaceEssentialsLapTimesReader(raceEssentialsDirectory, this)) {
                var result = _raceEssentialsStorage.GetCachedLapTimesList(reader);
                if (result != null) return result;

                await TracksManager.Instance.EnsureLoadedAsync();
                return _raceEssentialsStorage.UpdateCachedLapTimesList(reader);
            }
        }

        public void ClearCache() {
            DisposeHelper.Dispose(ref _acDbStorage);
            DisposeHelper.Dispose(ref _acStorageNew);
            DisposeHelper.Dispose(ref _sidekickStorage);
            DisposeHelper.Dispose(ref _raceEssentialsStorage);

            foreach (var file in Directory.GetFiles(FilesStorage.Instance.GetDirectory("Progress"), "Lap Times (*).data")) {
                if (!FileUtils.ArePathsEqual(Path.GetFileName(file), _cmStorage.Filename)) {
                    File.Delete(file);
                }
            }

            _lastUpdate = default(DateTime);
        }

        public IReadOnlyList<string> GetCarIds() {
            return CarsManager.Instance.WrappersList.Select(x => x.Id).ToList();
        }

        public IReadOnlyList<Tuple<string, string>> GetTrackIds() {
            return TracksManager.Instance.LoadedOnly
                                .SelectMany(x => x.MultiLayouts?.Select(y => new Tuple<string, string>(x.Id, y.LayoutId)) ??
                                        new[] { new Tuple<string, string>(x.Id, null) })
                                .ToList();
        }
    }
}
