using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.LapTimes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    internal class LapTimesStorage : Storage {
        private const string KeyPrefix = "laptime:";
        private const string KeyLastUpdated = "lastUpdated";

        private readonly string _displayName;

        public string Filename { get; }

        public DateTime? LastModified { get; private set; }

        private LapTimesStorage(string displayName, string filename, bool disableCompression)
                : base(filename, null, disableCompression) {
            Filename = filename;
            _displayName = displayName;
            LastModified = Get<DateTime?>(KeyLastUpdated);
        }

        public LapTimesStorage(string displayName, string sourceId)
                : this(displayName, FilesStorage.Instance.GetFilename("Progress", $"Lap Times ({sourceId}).data"), false) {}

        private string GetKey(string carId, string trackAnyId) {
            return KeyPrefix + carId + ":" + trackAnyId.Replace('/', '-');
        }

        private string Pack(LapTimeEntry entry) {
            return entry.EntryDate.ToUnixTimestamp().ToInvariantString() + ";" +
                    ((long)entry.LapTime.TotalMilliseconds).ToInvariantString();
        }

        private bool Unpack(string packed, out DateTime date, out TimeSpan lapTime) {
            var s = packed?.Split(';');
            if (s?.Length == 2 && long.TryParse(s[0], out var timestamp) && long.TryParse(s[1], out var milliseconds)) {
                date = timestamp.ToDateTime();
                lapTime = TimeSpan.FromMilliseconds(milliseconds);
                return true;
            }

            date = default;
            lapTime = default;
            return false;
        }

        [CanBeNull]
        private LapTimeEntry GetLapTime(string carId, string trackAcId, string packedValue) {
            return Unpack(packedValue, out var date, out var lapTime) ?
                    new LapTimeEntry(_displayName, carId, trackAcId, date, lapTime) : null;
        }

        [CanBeNull]
        public LapTimeEntry GetLapTime(string carId, string trackAcId) {
            return GetLapTime(carId, trackAcId, Get<string>(GetKey(carId, trackAcId)));
        }

        public IEnumerable<LapTimeEntry> GetCached() {
            return from p in this
                   where p.Key.StartsWith(KeyPrefix)
                   let s = p.Key.Split(':')
                   let e = s.Length == 3 ? GetLapTime(s[1], s[2], p.Value) : null
                   where e != null
                   select e;
        }

        public void Set([NotNull] LapTimeEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Set(GetKey(entry.CarId, entry.TrackAcId), Pack(entry));
        }

        public bool Remove([NotNull] string carId, [NotNull] string trackAnyId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));
            if (trackAnyId == null) throw new ArgumentNullException(nameof(trackAnyId));
            return Remove(GetKey(carId, trackAnyId));
        }

        public bool IsActual([NotNull] ILapTimesReader reader) {
            return LastModified.HasValue && reader.GetLastModified() <= LastModified.Value + TimeSpan.FromSeconds(1d);
        }

        [NotNull]
        public IReadOnlyList<LapTimeEntry> UpdateCached([NotNull] ILapTimesReader reader) {
            CleanUp(x => x.StartsWith(KeyPrefix));

            var list = reader.Import(_displayName).ToList();
            foreach (var entry in list) {
                Set(entry);
            }

            SyncLastModified(reader);
            return list;
        }

        [ItemNotNull]
        public Task<IReadOnlyList<LapTimeEntry>> UpdateCachedAsync([NotNull] ILapTimesReader reader) {
            return Task.Run(() => UpdateCached(reader));
        }

        public void SyncLastModified(ILapTimesReader reader) {
            LastModified = reader.GetLastModified();
            Set(KeyLastUpdated, LastModified.Value);
        }

        public void SyncLastModified() {
            LastModified = DateTime.Now;
            Set(KeyLastUpdated, LastModified.Value);
        }

        public bool IsBetter([NotNull] LapTimeEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var existing = GetLapTime(entry.CarId, entry.TrackAcId);
            return existing == null || existing.LapTime > entry.LapTime;
        }
    }
}