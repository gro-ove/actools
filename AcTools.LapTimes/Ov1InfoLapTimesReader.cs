using System;
using System.Collections.Generic;
using System.IO;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.LapTimes {
    public class Ov1InfoLapTimesReader : ILapTimesReader {
        private readonly string _ov1Directory;
        private readonly TrackIdsFixer _fixer;

        public Ov1InfoLapTimesReader(string ov1Directory, IAcIdsProvider provider) {
            _ov1Directory = ov1Directory;
            _fixer = new TrackIdsFixer(provider);
        }

        public IEnumerable<LapTimeEntry> Import(string sourceName) {
            var file = new FileInfo(Path.Combine(_ov1Directory, "userdata", "best_lap.ini"));
            if (!file.Exists) yield break;

            var ini = new IniFile(file.FullName);
            var date = file.CreationTime;

            foreach (var section in ini) {
                var trackLayoutId = _fixer.FixTrackId(section.Key);
                foreach (var pair in section.Value) {
                    var time = TimeSpan.FromMilliseconds(FlexibleParser.TryParseInt(pair.Value) ?? 0);
                    if (time.TotalSeconds < 1d) continue;
                    yield return new LapTimeEntry(sourceName, pair.Key.ToLowerInvariant(), trackLayoutId,
                            date, time);
                }
            }
        }

        public void Export(IEnumerable<LapTimeEntry> entries) {
            var ini = new IniFile(Path.Combine(_ov1Directory, "userdata", "best_lap.ini"));
            foreach (var entry in entries) {
                ini[entry.TrackAcId.ToLowerInvariant()].Set(entry.CarId.ToLowerInvariant(), entry.LapTime.TotalMilliseconds.Round());
            }
        }

        public void Remove(string carId, string trackId) {
            var ini = new IniFile(Path.Combine(_ov1Directory, "userdata", "best_lap.ini"));
            var section = ini[trackId.Replace('/', '-').ToLowerInvariant()];
            section.Remove(carId.ToLowerInvariant());
            ini.Save();
        }

        public bool CanExport => true;

        public DateTime GetLastModified() {
            var file = new FileInfo(Path.Combine(_ov1Directory, "userdata", "best_lap.ini"));
            return file.Exists ? file.CreationTime : default(DateTime);
        }

        public void Dispose() {}
    }
}