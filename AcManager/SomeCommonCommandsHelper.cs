using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.Objects;

namespace AcManager {
    public class SomeCommonCommandsHelper : ISomeCommonCommandsHelper {
        public void RunHotlap(CarObject car, TrackObjectBase track) {
            QuickDrive.RunHotlap(car, track: track);
        }

        public void SetupHotlap(CarObject car, TrackObjectBase track) {
            QuickDrive.ShowHotlap(car, track: track);
        }
    }
}