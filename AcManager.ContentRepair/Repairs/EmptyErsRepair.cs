using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair.Repairs {
    public class EmptyErsRepair : CarSimpleRepairBase {
        protected override void Fix(CarObject car, DataWrapper data) {
            data.Delete("ctrl_ers_0.ini");
            data.Delete("ers.ini");
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            return data.Contains("ctrl_ers_0.ini") && data.GetIniFile("ctrl_ers_0.ini").Keys.Count == 0 &&
                    data.Contains("ers.ini") && data.GetIniFile("ers.ini").Keys.Count == 0 ?
                    new CommonErrorSuggestion("Empty existing ERS-related files",
                            "It’s better to remove them to avoid logs overflooding.",
                            (p, c) => FixAsync(car, p, c)) { AffectsData = true }
                    : null;
        }
    }
}