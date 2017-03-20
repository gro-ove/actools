using System.Collections.Generic;

namespace AcTools.LapTimes {
    public class RaceEssentialsLapTimesReader : SidekickLapTimesReader {
        public RaceEssentialsLapTimesReader(string raceEssentialsDirectory, IAcIdsProvider provider) : base(raceEssentialsDirectory, provider) { }

        public new static readonly string SourceId = "Race Essentials";

        public override IEnumerable<LapTimeEntry> Import() {
            return GetEntries(SourceId);
        }
    }
}