using System;
using JetBrains.Annotations;

namespace AcManager.Tools.SharedMemory {
    public class AcSharedEventArgs : EventArgs {
        public AcSharedEventArgs([CanBeNull] AcShared previous, [CanBeNull] AcShared current, TimeSpan time) {
            Previous = previous;
            Current = current;
            Time = time;
        }

        [CanBeNull]
        public AcShared Previous { get; }

        [CanBeNull]
        public AcShared Current { get; }
        
        public TimeSpan Time { get; set; }
    }
}