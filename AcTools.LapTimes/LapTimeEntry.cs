using System;

namespace AcTools.LapTimes {
    public class LapTimeEntry {
        public LapTimeEntry(string sourceId, string carId, string trackId, DateTime entryDate, TimeSpan lapTime) {
            Source = sourceId;
            CarId = carId;
            LapTime = lapTime;
            TrackId = trackId;
            TrackAcId = trackId.Replace('/', '-');
            EntryDate = entryDate;
        }

        public LapTimeEntry(string sourceId, string carId, string trackId, string layoutId, DateTime entryDate, TimeSpan lapTime)
                : this(sourceId, carId, layoutId == null ? trackId : trackId + "-" + layoutId, entryDate, lapTime) {}

        public string CarId { get; }

        /// <summary>
        /// Could be “[track]”, “[track]/[layout]” or “[track]-[layout]”.
        /// </summary>
        public string TrackId { get; }

        /// <summary>
        /// Could be “[track]” or “[track]-[layout]”.
        /// </summary>
        public string TrackAcId { get; }

        public string Source { get; }

        public DateTime EntryDate { get; }

        public TimeSpan LapTime { get; }

        public bool Same(LapTimeEntry entry) {
            return string.Equals(entry.CarId, CarId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.TrackAcId, TrackAcId, StringComparison.OrdinalIgnoreCase);
        }
    }
}