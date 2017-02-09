using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Objects;

namespace AcManager.Tools.GameProperties {
    public class CarSpecificControlsPresetHelper : CarSpecificHelperBase {
        private const string BackupPostfix = "_backup_cm_carspec";

        public static void Revert() {
            new TemporaryFileReplacement(null, AcSettingsHolder.Controls.Filename, BackupPostfix).Revert();
        }

        protected override bool SetOverride(CarObject car) {
            return car.SpecificControlsPreset && car.ControlsPresetFilename != null &&
                    new TemporaryFileReplacement(car.ControlsPresetFilename, AcSettingsHolder.Controls.Filename, BackupPostfix).Apply();
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}