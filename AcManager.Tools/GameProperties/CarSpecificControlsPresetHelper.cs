using System;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class CarSpecificControlsPresetHelper : CarSpecificHelperBase {
        private const string BackupPostfix = "_backup_cm_carspec";

        public static void Revert(double? originalSteerValue = null) {
            if (new TemporaryFileReplacement(null, AcSettingsHolder.Controls.Filename, BackupPostfix).Revert()) return;

            if ((originalSteerValue.HasValue || AcSettingsHolder.Controls.WheelSteerScaleAutoAdjust) &&
                    AcSettingsHolder.Controls.WheelSteerScale != (originalSteerValue ?? 1d)) {
                AcSettingsHolder.Controls.WheelSteerScale = originalSteerValue ?? 1d;
                Logging.Debug($"Reset scale: {AcSettingsHolder.Controls.WheelSteerScale}");
            }
        }

        private double? _originalSteerScale;

        protected override bool SetOverride(CarObject car) {
            var specificControlsLoaded = false;

            if (car.SpecificControlsPreset && car.ControlsPresetFilename != null) {
                specificControlsLoaded |= new TemporaryFileReplacement(car.ControlsPresetFilename,
                        AcSettingsHolder.Controls.Filename, BackupPostfix, false).Apply();
            }

            if (AcSettingsHolder.Controls.WheelSteerScaleAutoAdjust && car.SteerLock.HasValue) {
                if (specificControlsLoaded) {
                    AcSettingsHolder.Controls.ForceReload();
                }

                var currentScale = AcSettingsHolder.Controls.WheelSteerScale;
                var scale = Math.Min(AcSettingsHolder.Controls.SteerAxleEntry.DegressOfRotation / car.SteerLock.Value, 1d);
                if (scale != currentScale) {
                    _originalSteerScale = currentScale;
                    AcSettingsHolder.Controls.WheelSteerScale = AcSettingsHolder.Controls.SteerAxleEntry.DegressOfRotation / car.SteerLock.Value;
                    AcSettingsHolder.Controls.SaveImmediately();
                    Logging.Debug($"Set scale: {AcSettingsHolder.Controls.WheelSteerScale}");
                }

                return true;
            }

            return specificControlsLoaded;
        }

        protected override void DisposeOverride() {
            Revert(_originalSteerScale);
        }
    }
}