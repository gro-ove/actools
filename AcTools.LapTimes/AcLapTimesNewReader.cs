using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.LapTimes {
    internal class TrackIdsFixer {
        private readonly IAcIdsProvider _provider;

        public TrackIdsFixer(IAcIdsProvider provider) {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private IReadOnlyList<Tuple<string, string>> _trackLayoutIds;

        public string FixTrackId(string kunosLayoutId) {
            if (kunosLayoutId.IndexOf('-') == -1) return kunosLayoutId;

            if (_trackLayoutIds == null) _trackLayoutIds = _provider.GetTrackIds();
            for (var i = _trackLayoutIds.Count - 1; i >= 0; i--) {
                var track = _trackLayoutIds[i];

                if (track.Item2 == null) {
                    if (string.Equals(track.Item1, kunosLayoutId, StringComparison.OrdinalIgnoreCase)) {
                        /* track found! awesome */
                        return kunosLayoutId;
                    }
                } else if (kunosLayoutId.StartsWith(track.Item1 + "-", StringComparison.OrdinalIgnoreCase)) {
                    var layoutId = kunosLayoutId.Substring(track.Item1.Length + 1);
                    if (string.Equals(track.Item2, layoutId, StringComparison.OrdinalIgnoreCase)) {
                        /* track with layout ID found! can’t believe my luck */
                        return track.Item1 + "/" + track.Item2;
                    }
                }
            }

            return kunosLayoutId;
        }
    }

    public class AcLapTimesNewReader : ILapTimesReader {
        private readonly string _filename;
        private readonly TrackIdsFixer _fixer;

        public AcLapTimesNewReader(string acDocumentsDirectory, IAcIdsProvider provider) {
            _filename = Path.Combine(acDocumentsDirectory, "personalbest.ini");
            _fixer = new TrackIdsFixer(provider);
        }

        public void Dispose() { }

        private bool TryToGuessCarAndTrack(string sectionName, out string carId, out string trackLayoutId) {
            var s = sectionName.Split(new[] { '@' }, 2);
            if (s.Length != 2 || string.IsNullOrWhiteSpace(s[0])) {
                carId = trackLayoutId = null;
                return false;
            }

            carId = s[0].ToLowerInvariant();
            trackLayoutId = _fixer.FixTrackId(s[1].ToLowerInvariant());
            return true;
        }

        public IEnumerable<LapTimeEntry> Import(string sourceName) {
            return new IniFile(_filename).Select(p => {
                var date = p.Value.GetDouble("DATE", 0);
                var time = p.Value.GetDouble("TIME", 0);
                if (time <= 1 || date <= 1) return null;

                if (!TryToGuessCarAndTrack(p.Key, out var carId, out var trackLayoutId)) return null;
                return new LapTimeEntry(sourceName, carId, trackLayoutId,
                        new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(date).ToLocalTime(),
                        TimeSpan.FromMilliseconds(time));
            }).NonNull();
        }

        public void Export(IEnumerable<LapTimeEntry> entries) {
            var iniFile = new IniFile(_filename);
            foreach (var entry in entries.ToList()) {
                var key = $"{entry.CarId.ToUpperInvariant()}@{entry.TrackAcId.ToUpperInvariant()}";
                var section = iniFile[key];
                section.Set("DATE", entry.EntryDate.ToMillisecondsTimestamp());
                section.Set("TIME", entry.LapTime.TotalMilliseconds.Round());
            }

            iniFile.Save();
        }

        public void Remove(string carId, string trackId) {
            var iniFile = new IniFile(_filename);
            var key = $"{carId.ToUpperInvariant()}@{trackId.Replace('/', '-').ToUpperInvariant()}";
            iniFile.Remove(key);
            iniFile.Save();
        }

        public DateTime GetLastModified() {
            var fi = new FileInfo(_filename);
            return fi.Exists ? fi.LastWriteTime : default(DateTime);
        }

        public bool CanExport => true;
        public bool CanStay => true;
    }
}