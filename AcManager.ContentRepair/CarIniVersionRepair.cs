using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair {
    public class CarIniVersionRepair : CarSimpleRepairBase {
        public static readonly CarDashCameraRepair Instance = new CarDashCameraRepair();

        protected override void Fix(CarObject car, DataWrapper data) {
            var ini = data.GetIniFile("car.ini");
            var section = ini["HEADER"];
            section.Set("VERSION", 2);
            ini.Save();
        }

        protected override ObsoletableAspect GetObsoletableAspect(CarObject car, DataWrapper data) {
            var ini = data.GetIniFile("car.ini");
            var section = ini["HEADER"];

            int version;
            if (int.TryParse(section.GetNonEmpty("VERSION"), out version) && version > 0 && version < 10) return null;

            return new ObsoletableAspect("Invalid [mono]HEADER/VERSION[/mono] parameter",
                    "A bit misleading, but this parameter is not to set mod’s version, but to specify used set of params.",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}