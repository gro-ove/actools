using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.LapTimes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    internal class LapTimesStorage : Storage {
        private const string KeyPrefix = "laptime:";
        private const string KeyLastUpdated = "lastUpdated";

        private readonly string _sourceId;

        public string Filename { get; }

        private LapTimesStorage(string filename, string sourceId)
                : base(filename, null, false, false) {
            Filename = filename;
            _sourceId = sourceId;
        }

        public LapTimesStorage(string sourceId)
                : this(FilesStorage.Instance.GetFilename("Progress", $"Lap Times ({sourceId}).data"), sourceId) {}

        private string GetKey(string carId, string trackAcId) {
            return KeyPrefix + carId + ":" + trackAcId.Replace('/', '-');
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
                    new LapTimeEntry(_sourceId, carId, trackAcId, date, lapTime) : null;
        }

        [CanBeNull]
        public LapTimeEntry GetLapTime(string carId, string trackAcId) {
            return GetLapTime(carId, trackAcId, GetString(GetKey(carId, trackAcId)));
        }

        public IEnumerable<LapTimeEntry> GetLapTimes() {
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

        [CanBeNull]
        public IReadOnlyList<LapTimeEntry> GetCachedLapTimesList(ILapTimesReader reader) {
            var lastUpdated = this.GetDateTime(KeyLastUpdated);
            return lastUpdated.HasValue && reader.GetLastModified() < lastUpdated.Value ?
                    GetLapTimes().ToList() : null;
        }

        [NotNull]
        public IReadOnlyList<LapTimeEntry> UpdateCachedLapTimesList(ILapTimesReader reader) {
            CleanUp(x => x.StartsWith(KeyPrefix));

            var list = reader.Import().ToList();
            foreach (var entry in list) {
                Set(entry);
            }

            this.Set(KeyLastUpdated, reader.GetLastModified());
            return list;
        }

        [NotNull]
        public IReadOnlyList<LapTimeEntry> GetLapTimesList(ILapTimesReader reader) {
            return GetCachedLapTimesList(reader) ?? UpdateCachedLapTimesList(reader);
        }

        public bool IsBetter([NotNull] LapTimeEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var existing = GetLapTime(entry.CarId, entry.TrackAcId);
            return existing == null || existing.LapTime > entry.LapTime;
        }
    }
}