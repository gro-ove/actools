using System;
using AcTools.Utils;
using System.Diagnostics;
using System.IO;
using AcTools.DataFile;

namespace AcTools.Processes {
    public partial class Showroom {
        [Obsolete]
        public static void Start(string acRoot, string carName, string skinName, string trackName, string filterName = null) {
            PrepareIni(carName, skinName, trackName);

            string originalFilter = null;
            if (filterName != null) {
                originalFilter = GetCurrentFilterIni();
                SetCurrentFilterIni(filterName);
            }

            var process = Process.Start(new ProcessStartInfo() {
                WorkingDirectory = acRoot,
                FileName = "acShowroom.exe"
            });

            if (process != null) {
                process.WaitForExit();
            }

            if (originalFilter != null) {
                SetCurrentFilterIni(originalFilter);
            }
        }
    }
}
