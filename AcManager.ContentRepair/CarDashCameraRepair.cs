using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair {
    public class CarDashCameraRepair : CarSimpleRepairBase {
        public static readonly CarDashCameraRepair Instance = new CarDashCameraRepair();

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

        protected override ObsoletableAspect GetObsoletableAspect(CarObject car, DataWrapper data) {
            var dashCam = data.GetIniFile(@"dash_cam.ini");
            if (!dashCam.IsEmptyOrDamaged()) return null;
            return new ObsoletableAspect("Dash camera is not set", "By default, its position is below the car, which is quite unusable.",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}