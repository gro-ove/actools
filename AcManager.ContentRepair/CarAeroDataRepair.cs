using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair {
    public class CarAeroDataRepair : CarSimpleRepairBase {
        public static readonly CarAeroDataRepair Instance = new CarAeroDataRepair();

        protected override void Fix(CarObject car, DataWrapper data) {
            var aero = data.GetIniFile("aero.ini");
            aero.Remove("DATA");
            aero.Save();
        }

        protected override ObsoletableAspect GetObsoletableAspect(CarObject car, DataWrapper data) {
            if (car.AcdData?.GetIniFile(@"aero.ini").ContainsKey(@"DATA") != true) return null;
            return new ObsoletableAspect("Obsolete section DATA in aero.ini", "How old is this mod? ಠ_ಠ", 
                    (p, c) => FixAsync(car, p, c)) {
                        AffectsData = true
                    };
        }
    }
}