using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.LapTimes {
    public class AcLapTimesNewReader : ILapTimesReader {
        public static readonly string SourceId = "AC New";

        private readonly string _filename;
        private readonly IAcIdsProvider _provider;
        private IReadOnlyList<Tuple<string, string>> _trackLayoutIds;

        public AcLapTimesNewReader(string acDocumentsDirectory, IAcIdsProvider provider) {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            _filename = Path.Combine(acDocumentsDirectory, "personalbest.ini");
            _provider = provider;
        }

        public void Dispose() { }

        private bool TryToGuessCarAndTrack(string sectionName, out string carId, out string trackLayoutId) {
            var s = sectionName.Split(new[] { '@' }, 2);
            if (s.Length != 2 || string.IsNullOrWhiteSpace(s[0])) {
                carId = trackLayoutId = null;
                return false;
            }

            carId = s[0].ToLowerInvariant();
            trackLayoutId = s[1].ToLowerInvariant();
            if (trackLayoutId.IndexOf('-') == -1) return true;

            if (_trackLayoutIds == null) _trackLayoutIds = _provider.GetTrackIds();
            foreach (var track in _trackLayoutIds) {
                if (track.Item2 == null) {
                    if (string.Equals(track.Item1, trackLayoutId, StringComparison.OrdinalIgnoreCase)) {
                        /* track found! awesome */
                        return true;
                    }
                } else if (trackLayoutId.StartsWith(track.Item1 + "-", StringComparison.OrdinalIgnoreCase)) {
                    var layoutId = trackLayoutId.Substring(track.Item1.Length + 1);
                    if (string.Equals(track.Item2, layoutId, StringComparison.OrdinalIgnoreCase)) {
                        /* track with layout ID found! can’t believe my luck */
                        trackLayoutId = track.Item1 + "/" + track.Item2;
                        return true;
                    }
                }
            }

            carId = null;
            trackLayoutId = null;
            return false;
        }

        public IEnumerable<LapTimeEntry> Import() {
            return new IniFile(_filename).Select(p => {
                var date = p.Value.GetDouble("DATE", 0);
                var time = p.Value.GetDouble("TIME", 0);
                if (time <= 1 || date <= 1) return null;

                string carId, trackLayoutId;
                if (!TryToGuessCarAndTrack(p.Key, out carId, out trackLayoutId)) return null;

                return new LapTimeEntry(SourceId, carId, trackLayoutId,
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

        public DateTime GetLastModified() {
            var fi = new FileInfo(_filename);
            return fi.Exists ? fi.LastWriteTime : default(DateTime);
        }
    }
}