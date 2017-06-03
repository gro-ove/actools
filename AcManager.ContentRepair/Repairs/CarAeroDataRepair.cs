using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair.Repairs {
    public class CarAeroDataRepair : CarSimpleRepairBase {
        public static readonly CarAeroDataRepair Instance = new CarAeroDataRepair();

        protected override void Fix(CarObject car, DataWrapper data) {
            var aero = data.GetIniFile("aero.ini");
            aero.Remove("DATA");
            aero.Save();
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            if (data.GetIniFile(@"aero.ini").ContainsKey(@"DATA") != true) return null;
            return new ContentObsoleteSuggestion("Obsolete section DATA in aero.ini", "How old is this mod? ಠ_ಠ",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}