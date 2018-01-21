using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static class AcProcess {
        private static bool IsGameProcess(Process process) {
            var directory = Path.GetDirectoryName(process.GetFilenameSafe());
            return directory == null || AcPaths.IsAcRoot(directory);
        }

        private static Process _lastFound;

        [CanBeNull]
        public static Process TryToFind() {
            if (_lastFound != null && !_lastFound.HasExitedSafe()) return _lastFound;
            var processes = Process.GetProcesses();
            _lastFound = processes.FirstOrDefault(x => (x.ProcessName == "acs" || x.ProcessName == "acs_x86") && IsGameProcess(x)) ??
                    processes.FirstOrDefault(x => x.ProcessName.IndexOf(@"acs", StringComparison.OrdinalIgnoreCase) != -1 && IsGameProcess(x));
            if (_lastFound != null) {
                AcToolsLogging.Write("Process found");
            }
            return _lastFound;
        }
    }
}