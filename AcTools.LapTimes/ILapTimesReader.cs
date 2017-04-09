using System;
using System.Collections.Generic;

namespace AcTools.LapTimes {
    public interface ILapTimesReader : IDisposable {
        IEnumerable<LapTimeEntry> Import(string sourceName);

        void Export(IEnumerable<LapTimeEntry> entries);

        void Remove(string carId, string trackId);

        bool CanExport { get; }

        DateTime GetLastModified();
    }
}