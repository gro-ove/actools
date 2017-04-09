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
            LastModified = this.GetDateTime(KeyLastUpdated);
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
            long timestamp, milliseconds;
            var s = packed?.Split(';');
            if (s?.Length == 2 && long.TryParse(s[0], out timestamp) && long.TryParse(s[1], out milliseconds)) {
                date = timestamp.ToDateTime();
                lapTime = TimeSpan.FromMilliseconds(milliseconds);
                return true;
            }

            date = default(DateTime);
            lapTime = default(TimeSpan);
            return false;
        }

        [CanBeNull]
        private LapTimeEntry GetLapTime(string carId, string trackAcId, string packedValue) {
            DateTime date;
            TimeSpan lapTime;
            return Unpack(packedValue, out date, out lapTime) ?
                    new LapTimeEntry(_displayName, carId, trackAcId, date, lapTime) : null;
        }

        [CanBeNull]
        public LapTimeEntry GetLapTime(string carId, string trackAcId) {
            return GetLapTime(carId, trackAcId, GetString(GetKey(carId, trackAcId)));
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
            this.Set(GetKey(entry.CarId, entry.TrackAcId), Pack(entry));
        }

        public bool Remove([NotNull] string carId, [NotNull] string trackAnyId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));
            if (trackAnyId == null) throw new ArgumentNullException(nameof(trackAnyId));
            return Remove(GetKey(carId, trackAnyId));
        }

        public bool IsActual(ILapTimesReader reader) {
            return LastModified.HasValue && reader.GetLastModified() < LastModified.Value;
        }

        [NotNull]
        public IReadOnlyList<LapTimeEntry> UpdateCached(ILapTimesReader reader) {
            CleanUp(x => x.StartsWith(KeyPrefix));

            var list = reader.Import(_displayName).ToList();
            foreach (var entry in list) {
                Set(entry);
            }

            SyncLastModified(reader);
            return list;
        }

        [ItemNotNull]
        public Task<IReadOnlyList<LapTimeEntry>> UpdateCachedAsync(ILapTimesReader reader) {
            return Task.Run(() => UpdateCached(reader));
        }

        public void SyncLastModified(ILapTimesReader reader) {
            LastModified = reader.GetLastModified();
            this.Set(KeyLastUpdated, LastModified.Value);
        }

        public void SyncLastModified() {
            LastModified = DateTime.Now;
            this.Set(KeyLastUpdated, LastModified.Value);
        }

        public bool IsBetter([NotNull] LapTimeEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var existing = GetLapTime(entry.CarId, entry.TrackAcId);
            return existing == null || existing.LapTime > entry.LapTime;
        }
    }
}