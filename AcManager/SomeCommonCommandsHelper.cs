using System.Threading.Tasks;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.Objects;

namespace AcManager {
    public class SomeCommonCommandsHelper : ISomeCommonCommandsHelper {
        public Task<bool> RunHotlapAsync(CarObject car, TrackObjectBase track) {
            return QuickDrive.RunAsync(car, track: track, mode: QuickDrive.ModeHotlap);
        }

        public void SetupHotlap(CarObject car, TrackObjectBase track) {
            QuickDrive.Show(car, track: track, mode: QuickDrive.ModeHotlap);
        }
    }
}