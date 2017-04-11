using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair.Repairs {
    // ERROR: Car fc1_2015_brackley_gp cannot have setup items for ROD_LENGTH_HF when a front suspension is also present
    public class RodLengthHeaveRepair : CarSimpleRepairBase {
        private enum Suspension { Front, Rear }

        private static bool IsSuspensionPresent(DataWrapper data, Suspension suspension) {
            var suspensions = data.GetIniFile("suspensions.ini");
            var section = suspensions[suspension == Suspension.Front ? "FRONT" : "REAR"];
            return section.GetDouble("ROD_LENGTH", 0d) != 0d ||
                    section.GetDouble("SPRING_RATE", 0d) != 0d ||
                    section.GetDouble("PROGRESSIVE_SPRING_RATE", 0d) != 0d /*&&
                    section.GetDouble("DAMP_BUMP", 0d) == 0d &&
                    section.GetDouble("DAMP_FAST_BUMP", 0d) == 0d &&
                    section.GetDouble("DAMP_FAST_BUMPTHRESHOLD", 0d) == 0d &&
                    section.GetDouble("DAMP_REBOUND", 0d) == 0d &&
                    section.GetDouble("DAMP_FAST_REBOUND", 0d) == 0d &&
                    section.GetDouble("DAMP_FAST_BUMPTHRESHOLD", 0d) == 0d*/;

        }

        protected override void Fix(CarObject car, DataWrapper data) {
            var setupIni = data.GetIniFile(@"setup.ini");

            var front = setupIni.ContainsKey("ROD_LENGTH_HF") && IsSuspensionPresent(data, Suspension.Front);
            var rear = setupIni.ContainsKey("ROD_LENGTH_HR") && IsSuspensionPresent(data, Suspension.Rear);

            if (front) {
                setupIni["__ROD_LENGTH_HF__BACKUP"] = setupIni["ROD_LENGTH_HF"];
                setupIni.Remove("ROD_LENGTH_HF");
            }

            if (rear) {
                setupIni["__ROD_LENGTH_HR__BACKUP"] = setupIni["ROD_LENGTH_HR"];
                setupIni.Remove("ROD_LENGTH_HR");
            }

            setupIni.Save();
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var setupIni = data.GetIniFile(@"setup.ini");

            var front = setupIni.ContainsKey("ROD_LENGTH_HF") && IsSuspensionPresent(data, Suspension.Front);
            var rear = setupIni.ContainsKey("ROD_LENGTH_HR") && IsSuspensionPresent(data, Suspension.Rear);
            if (!front && !rear) return null;

            return new ContentObsoleteSuggestion("Heave’s length can’t be customized with existing suspension",
                    "Some time ago, it was possible, but now AC just crashes if there is ROD_LENGTH_HF or ROD_LENGTH_HR section in setup.ini.",
                    (p, c) => FixAsync(car, p, c)) {
                        AffectsData = true
                    };
        }
    }
}