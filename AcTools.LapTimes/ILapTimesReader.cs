using System;
using System.Collections.Generic;

namespace AcTools.LapTimes {
    public interface ILapTimesReader : IDisposable {
        IEnumerable<LapTimeEntry> GetEntries();

        DateTime GetLastModified();
    }
}