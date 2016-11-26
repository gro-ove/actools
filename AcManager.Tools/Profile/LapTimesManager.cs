using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.LapTimes;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    public class LapTimesManager : NotifyPropertyChanged, IAcIdsProvider {
        public static readonly string SourceId = @"Content Manager";

        private static LapTimesManager _instance;

        public static LapTimesManager Instance => _instance ?? (_instance = new LapTimesManager());

        private LapTimesStorage _cmStorage;
        private LapTimesStorage _acStorage;
        private LapTimesStorage _sidekickStorage;

        public LapTimesManager() {
            Entries = new BetterObservableCollection<LapTimeEntry>();
        }

        public void SetListener() {
            GameWrapper.Ended += OnRaceFinished;
        }

        private void OnRaceFinished(object sender, GameEndedArgs e) {
            var basic = e.StartProperties.BasicProperties;
            var bestLap = e.Result?.GetExtraByType<Game.ResultExtraBestLap>();
            var time = basic?.CarId == null || basic.TrackId == null || bestLap == null ||
                    bestLap.IsCancelled ? null : bestLap?.Time;

            if (SettingsHolder.Drive.WatchForSharedMemory) {
                var sharedTime = PlayerStatsManager.Instance.Last?.BestLap;
                if (sharedTime.HasValue) {
                    time = sharedTime.Value;
                }
            }
            
            if (time.HasValue) {
                AddEntry(new LapTimeEntry(SourceId,
                        basic.CarId, basic.TrackId, basic.TrackConfigurationId,
                        DateTime.Now, time.Value));
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
            if (_acStorage != null) return;
            _acStorage = new LapTimesStorage(AcLapTimesReader.SourceId);
        }

        private void InitializeSidekick() {
            if (_sidekickStorage != null) return;
            _sidekickStorage = new LapTimesStorage(SidekickLapTimesReader.SourceId);
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

            var cmEntries = ReadCmEntries();
            var acEntries = await ReadAcEntriesAsync();
            var sidekickEntries = await ReadSidekickEntriesAsync();
            Entries.ReplaceEverythingBy(KeepBetterOnes(
                    cmEntries.Concat(acEntries).Concat(sidekickEntries)
                             .OrderByDescending(x => x.EntryDate)));
        }

        private IEnumerable<LapTimeEntry> ReadCmEntries() {
            InitializeCm();
            return _cmStorage.GetLapTimes();
        }

        private async Task<IReadOnlyList<LapTimeEntry>> ReadAcEntriesAsync() {
            InitializeAc();
            return await Task.Run(() => {
                using (var reader = new AcLapTimesReader(FileUtils.GetDocumentsDirectory())) {
                    return _acStorage.GetLapTimesList(reader);
                }
            });
        }
        
        private async Task<IReadOnlyList<LapTimeEntry>> ReadSidekickEntriesAsync() {
            InitializeSidekick();
            var sidekickDirectory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), "Sidekick");
            using (var reader = new SidekickLapTimesReader(sidekickDirectory, this)) {
                var result = _sidekickStorage.GetCachedLapTimesList(reader);
                if (result != null) return result;

                await TracksManager.Instance.EnsureLoadedAsync();
                return _sidekickStorage.UpdateCachedLapTimesList(reader);
            }
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
