using AcManager.Tools.Objects;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Repairs {
    [UsedImplicitly]
    public class CarDashCameraRepair : CarSimpleRepairBase {
        protected override void Fix(CarObject car, DataWrapper data) {
            var dashCam = data.GetIniFile("dash_cam.ini");
            var section = dashCam["DASH_CAM"];
            var driverEyes = data.GetIniFile("car.ini")["GRAPHICS"].GetVector3("DRIVEREYES");
            driverEyes[1] += 0.05;
            driverEyes[2] += 0.15;
            section.Set("POS", driverEyes);
            section.Set("EXP", 27);
            dashCam.Save();
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var dashCam = data.GetIniFile(@"dash_cam.ini");
            if (!dashCam.IsEmptyOrDamaged()) return null;
            return new ContentObsoleteSuggestion("Dash camera is not set", "By default, its position is below the car, which is quite unusable.",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}