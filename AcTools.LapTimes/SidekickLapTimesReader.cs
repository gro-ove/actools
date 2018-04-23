using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AcTools.Utils.Helpers;

namespace AcTools.LapTimes {
    public class SidekickLapTimesReader : ILapTimesReader {
        // http://svn.python.org/projects/python/tags/r32/Lib/pickle.py
        private static bool ReadPickle(string filename, out long result) {
            var data = File.ReadAllBytes(filename);
            if (data.Length < 3 || data[0] != 0x80 || data[1] != 3) {
                result = 0;
                return false;
            }

            string s;
            switch (data[2]) {
                case (byte)'N':
                    result = 0;
                    return true;
                case (byte)'J':
                    result = BitConverter.ToInt32(data, 3);
                    return true;
                case (byte)'F':
                    result = (long)(ReadLine(out s) ? float.Parse(s) : BitConverter.ToSingle(data, 3));
                    return true;
                case (byte)'I':
                    result = ReadLine(out s) ? int.Parse(s) : BitConverter.ToInt32(data, 3);
                    return true;
                case (byte)'L':
                    result = ReadLine(out s) ? long.Parse(s) : BitConverter.ToInt64(data, 3);
                    return true;
                case (byte)'G':
                    result = (long)BitConverter.ToDouble(data.Skip(3).Take(8).Reverse().ToArray(), 0);
                    return true;
                case (byte)'M':
                    result = BitConverter.ToUInt16(data, 3);
                    return true;
                case (byte)'K':
                    result = data[3];
                    return true;
            }

            result = 0;
            return false;

            // Because I messed up
            bool ReadLine(out string piece) {
                var end = Array.IndexOf(data, (byte)'\n', 3);
                piece = end == -1 ? null : Encoding.ASCII.GetString(data, 3, end - 3);
                return end > -1;
            }
        }

        private static void WritePickle(string filename, long value) {
            using (var stream = File.Open(filename, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream)) {
                writer.Write(new byte[] { 0x80, 3, (byte)'L' });
                writer.Write(Encoding.ASCII.GetBytes(value.ToString()));
                writer.Write((byte)'\n');
                writer.Write((byte)'.');
            }
        }

        private readonly string _sidekickDirectory;
        private readonly IAcIdsProvider _provider;
        private IReadOnlyList<string> _carIds;
        private IReadOnlyList<Tuple<string, string>> _trackLayoutIds;

        public SidekickLapTimesReader(string sidekickDirectory, IAcIdsProvider provider) {
            /* TODO: Convert IDs to lower case? */
            _sidekickDirectory = sidekickDirectory;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private string TryToGuessTrack(string acTrackId) {
            if (acTrackId.IndexOf('/') != -1) return acTrackId;
            if (_trackLayoutIds == null) _trackLayoutIds = _provider.GetTrackIds();

            foreach (var track in _trackLayoutIds) {
                if (track.Item2 == null) {
                    if (string.Equals(track.Item1, acTrackId, StringComparison.OrdinalIgnoreCase)) {
                        /* track found! awesome */
                        return acTrackId;
                    }
                } else if (acTrackId.StartsWith(track.Item1, StringComparison.OrdinalIgnoreCase)) {
                    var layoutId = acTrackId.Substring(track.Item1.Length);
                    if (string.Equals(track.Item2, layoutId, StringComparison.OrdinalIgnoreCase)) {
                        /* track with layout ID found! can’t believe my luck */
                        return track.Item1 + "/" + track.Item2;
                    }
                }
            }

            return null;
        }

        private bool TryToGuessCarAndTrack(string filename, out string carId, out string trackLayoutId) {
            filename = Path.GetFileName(filename)?.ApartFromLast("_pb.ini", StringComparison.OrdinalIgnoreCase);
            if (filename != null) {
                if (_carIds == null) _carIds = _provider.GetCarIds();

                var i = -1;
                while (true) {
                    i = filename.IndexOf('_', i + 1);
                    if (i == -1) break;

                    carId = filename.Substring(0, i);
                    trackLayoutId = filename.Substring(i + 1);
                    if (!_carIds.Contains(carId, StringComparer.OrdinalIgnoreCase)) continue;
                    /* car’s found! hopefully */

                    trackLayoutId = TryToGuessTrack(trackLayoutId);
                    if (trackLayoutId != null) return true;
                }
            }

            carId = null;
            trackLayoutId = null;
            return false;
        }

        public IEnumerable<LapTimeEntry> Import(string sourceName) {
            var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
            if (!directory.Exists) yield break;

            foreach (var file in directory.GetFiles("*_pb.ini")) {
                TimeSpan time;
                var timeValue = 0L;

                try {
                    if (file.Length > 1000 || !ReadPickle(file.FullName, out timeValue) || timeValue == 0) continue;
                    time = TimeSpan.FromMilliseconds(timeValue);
                } catch (OverflowException e) {
                    AcToolsLogging.Write($"Can’t read {file.Name}: {e.Message} (value: {timeValue})");
                    continue;
                } catch (Exception e) {
                    AcToolsLogging.Write($"Can’t read {file.Name}: {e}");
                    continue;
                }

                if (TryToGuessCarAndTrack(file.FullName, out var carId, out var trackLayoutId)) {
                    yield return new LapTimeEntry(sourceName, carId, trackLayoutId,
                            file.LastWriteTime, time);
                }
            }
        }

        private string GetFilename(string carId, string trackId) {
            var name = $"{carId}_{(TryToGuessTrack(trackId) ?? trackId).Replace("/", "")}_pb.ini";
            return Path.Combine(_sidekickDirectory, "personal_best", name);
        }

        public void Export(IEnumerable<LapTimeEntry> entries) {
            var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
            if (!directory.Exists) {
                Directory.CreateDirectory(directory.FullName);
            }

            foreach (var entry in entries.ToList()) {
                var filename = GetFilename(entry.CarId, entry.TrackId);
                WritePickle(filename, (long)entry.LapTime.TotalMilliseconds);
                new FileInfo(filename).LastWriteTime = entry.EntryDate;
            }
        }

        public void Remove(string carId, string trackId) {
            var filename = GetFilename(carId, trackId);
            AcToolsLogging.Write(filename);
            if (File.Exists(filename)) {
                File.Delete(filename);
            }
        }

        private DateTime _lastModified;
        private DateTime _lastModifiedReadAt;

        public DateTime GetLastModified() {
            var n = DateTime.Now;
            var p = n - _lastModifiedReadAt;
            if (p.TotalMinutes > 5) {
                var directory = new DirectoryInfo(Path.Combine(_sidekickDirectory, "personal_best"));
                if (!directory.Exists) return default(DateTime);

                _lastModifiedReadAt = n;
                _lastModified = directory.GetFiles("*_pb.ini").Select(f => f.LastWriteTime)
                                         .OrderByDescending(f => f).Cast<DateTime?>().FirstOrDefault() ?? default(DateTime);
            }

            return _lastModified;
        }

        public void Dispose() { }

        public bool CanExport => true;
        public bool CanStay => true;
    }
}