using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.LapTimes {
    public class SidekickLapTimesReader : ILapTimesReader {
        // http://svn.python.org/projects/python/tags/r32/Lib/pickle.py
        private static bool ReadPickle(BinaryReader reader, out long result) {
            if (reader.ReadByte() != 128 || reader.ReadByte() != 3) {
                result = 0;
                return false;
            }

            switch (reader.ReadByte()) {
                case (byte)'F':
                    result = (long)reader.ReadSingle();
                    return true;
                case (byte)'N':
                    result = 0;
                    return true;
                case (byte)'I':
                case (byte)'J':
                    result = reader.ReadInt32();
                    return true;
                case (byte)'L':
                    result = reader.ReadInt64();
                    return true;
                case (byte)'M':
                    result = reader.ReadUInt16();
                    return true;
                case (byte)'K':
                    result = reader.ReadByte();
                    return true;
            }

            result = 0;
            return false;
        }

        private static void WritePickle(BinaryWriter writer, long value) {
            writer.Write(new byte[] { 128, 3, (byte)'L' });
            writer.Write(value);
        }

        private readonly string _sidekickDirectory;
        private readonly IAcIdsProvider _provider;
        private IReadOnlyList<string> _carIds;
        private IReadOnlyList<Tuple<string, string>> _trackLayoutIds;

        public SidekickLapTimesReader(string sidekickDirectory, IAcIdsProvider provider) {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            /* TODO: Convert IDs to lower case? */
            _sidekickDirectory = sidekickDirectory;
            _provider = provider;
        }
        
        private bool TryToGuessCarAndTrack(string filename, out string carId, out string trackLayoutId) {
            filename = Path.GetFileName(filename)?.ApartFromLast("_pb.ini", StringComparison.OrdinalIgnoreCase);
            if (filename != null) {
                if (_carIds == null) _carIds = _provider.GetCarIds();
                if (_trackLayoutIds == null) _trackLayoutIds = _provider.GetTrackIds();

                var i = -1;
                while (true) {
                    i = filename.IndexOf('_', i + 1);
                    if (i == -1) break;

                    carId = filename.Substring(0, i);
                    trackLayoutId = filename.Substring(i + 1);
                    if (!_carIds.Contains(carId, StringComparer.OrdinalIgnoreCase)) continue;
                    /* car found! hopefully */

                    foreach (var track in _trackLayoutIds) {
                        if (track.Item2 == null) {
                            if (string.Equals(track.Item1, trackLayoutId, StringComparison.OrdinalIgnoreCase)) {
                                /* track found! awesome */
                                return true;
                            }
                        } else if (trackLayoutId.StartsWith(track.Item1, StringComparison.OrdinalIgnoreCase)) {
                            var layoutId = trackLayoutId.Substring(track.Item1.Length);
                            if (string.Equals(track.Item2, layoutId, StringComparison.OrdinalIgnoreCase)) {
                                /* track with layout ID found! can’t believe my luck */
                                trackLayoutId = track.Item1 + "/" + track.Item2;
                                return true;
                            }
                        }
                    }
                }
            }

            carId = null;
            trackLayoutId = null;
            return false;
        }

        protected IEnumerable<LapTimeEntry> GetEntries(string sourceId) {
            var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
            if (!directory.Exists) yield break;

            foreach (var file in directory.GetFiles("*_pb.ini")) {
                long time;

                using (var stream = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream)) {
                    if (!ReadPickle(reader, out time) || time == 0) continue;
                }

                string carId, trackLayoutId;
                if (TryToGuessCarAndTrack(file.FullName, out carId, out trackLayoutId)) {
                    yield return new LapTimeEntry(sourceId, carId, trackLayoutId,
                            file.CreationTime, TimeSpan.FromMilliseconds(time));
                }
            }
        }

        public static readonly string SourceId = "Sidekick";

        public virtual IEnumerable<LapTimeEntry> Import() {
            return GetEntries(SourceId);
        }

        public void Export(IEnumerable<LapTimeEntry> entries) {
            var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
            if (!directory.Exists) {
                Directory.CreateDirectory(directory.FullName);
            }

            foreach (var entry in entries.ToList()) {
                var name = $"{entry.CarId}_{entry.TrackId.Replace("/", "")}_pb.ini";
                var filename = Path.Combine(directory.FullName, name);

                using (var stream = File.Open(filename, FileMode.Create, FileAccess.Write))
                using (var reader = new BinaryWriter(stream)) {
                    WritePickle(reader, (long)entry.LapTime.TotalMilliseconds);
                }

                new FileInfo(filename).CreationTime = entry.EntryDate;
            }
        }

        public DateTime GetLastModified() {
            var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
            return directory.Exists ? directory.GetFiles("*_pb.ini").Select(f => f.LastWriteTime)
                                               .OrderByDescending(f => f).Cast<DateTime?>().FirstOrDefault() ?? default(DateTime) : default(DateTime);
        }

        public void Dispose() {}
    }
}