using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Utils.Helpers;

namespace AcTools.LapTimes {
    public interface IAcIdsProvider {
        IReadOnlyList<string> GetCarIds();

        IReadOnlyList<Tuple<string, string>> GetTrackIds();
    }

    public class SidekickLapTimesReader : ILapTimesReader {
        public static readonly string SourceId = "Sidekick";

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
                    if (!_carIds.Contains(carId)) continue;
                    /* car found! hopefully */

                    foreach (var track in _trackLayoutIds) {
                        if (track.Item2 == null) {
                            if (track.Item1 == trackLayoutId) {
                                /* track found! awesome */
                                return true;
                            }
                        } else if (trackLayoutId.StartsWith(track.Item1)) {
                            var layoutId = trackLayoutId.Substring(track.Item1.Length);
                            if (track.Item2 == layoutId) {
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

        public IEnumerable<LapTimeEntry> GetEntries() {
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
                    yield return new LapTimeEntry(SourceId, carId, trackLayoutId,
                            file.CreationTime, TimeSpan.FromMilliseconds(time));
                }
            }
        }

        public DateTime GetLastModified() {
            var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
            return directory.Exists ? directory.GetFiles("*_pb.ini").Select(f => f.LastWriteTime)
                                               .OrderByDescending(f => f).First() : default(DateTime);
        }

        public void Dispose() {}
    }
}