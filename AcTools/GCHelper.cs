using System;
using System.ComponentModel;
using System.Runtime;

namespace AcTools {
    public static class GCHelper {
        public static void CleanUp() {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
