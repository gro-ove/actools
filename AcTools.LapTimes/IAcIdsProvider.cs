using System;
using System.Collections.Generic;

namespace AcTools.LapTimes {
    public interface IAcIdsProvider {
        IReadOnlyList<string> GetCarIds();

        IReadOnlyList<Tuple<string, string>> GetTrackIds();
    }
}