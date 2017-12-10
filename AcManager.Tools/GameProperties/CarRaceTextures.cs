using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class CarRaceTextures : Game.RaceIniProperties {
        // Items might repeat, use .Distinct()
        [ItemCanBeNull]
        private static IEnumerable<string> GetCarIds(IniFile iniFile) {
            yield return iniFile["RACE"].GetNonEmpty("MODEL");
            foreach (var car in iniFile.GetSections("CAR", 1)) {
                yield return car.GetNonEmpty("MODEL");
            }
        }

        public override void Set(IniFile file) {
            var s = Stopwatch.StartNew();

            try {
                var trackId = file["RACE"].GetNonEmpty("TRACK");
                var configurationId = file["RACE"].GetNonEmpty("CONFIG_TRACK");
                if (trackId == null) return;

                var track = TracksManager.Instance.GetLayoutById(trackId, configurationId);
                foreach (var car in GetCarIds(file).NonNull().Distinct().Select(x => CarsManager.Instance.GetById(x)).NonNull()) {
                    car.PrepareRaceTextures(track);
                }
            } finally {
                Logging.Write($"Time taken: {s.Elapsed.TotalMilliseconds:F2} ms");
            }
        }
    }
}