using System;
using System.Collections.Generic;

namespace AcTools.LapTimes {
    public interface ILapTimesReader : IDisposable {
        IEnumerable<LapTimeEntry> Import();

        void Export(IEnumerable<LapTimeEntry> entries);

        DateTime GetLastModified();
    }
}