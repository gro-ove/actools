using System;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.WheelAngles;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class CarSpecificControlsPresetHelper : CarSpecificHelperBase {
        private const string BackupPostfix = "_backup_cm_carspec";

        public static void Revert() {
            var controlsFilename = AcPaths.GetCfgControlsFilename();
            var ini = new IniFile(controlsFilename);
            var iniChanged = Revert(ini, out var originalLock);
            if (originalLock.HasValue) {
                try {
                    GetSteerLockSetter(ini)?.Apply(originalLock.Value, true, out _);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Failed to apply hardware lock", e);
                }
            }

            if (!new TemporaryFileReplacement(null, controlsFilename, BackupPostfix).Revert() && iniChanged) {
                ini.Save();
            }
        }

        protected override bool SetOverride(CarObject car) {
            var controlsFilename = AcPaths.GetCfgControlsFilename();
            var specificControlsLoaded = car.SpecificControlsPreset && car.ControlsPresetFilename != null
                    && new TemporaryFileReplacement(car.ControlsPresetFilename, controlsFilename, BackupPostfix, false).Apply();

            var ini = new IniFile(controlsFilename);
            var iniChanged = Revert(ini, out _);

            var extra = ini["__EXTRA_CM"];
            var steer = ini["STEER"];

            var carSteerLock = car.SteerLock ?? 0d;
            if (carSteerLock < 40) {
                Logging.Debug("Invalid car steer lock");
                Revert();
                return specificControlsLoaded;
            }

            Logging.Debug($"Car steer lock: {carSteerLock}°");
            var hardLock = extra.GetBool("HARDWARE_LOCK", false);
            var currentDegrees = steer.GetInt("LOCK", 900);

            if (hardLock) {
                Logging.Debug($"Hardware lock enabled, controls lock: {currentDegrees}");
            }

            if (hardLock && currentDegrees > carSteerLock) {
                var lockSetter = GetSteerLockSetter(ini);
                Logging.Debug(lockSetter == null
                        ? "Lock setter for provided device not found"
                        : $"Lock setter: {lockSetter.MinimumSteerLock}…{lockSetter.MaximumSteerLock}");

                if (lockSetter != null && carSteerLock < lockSetter.MaximumSteerLock) {
                    var newDegrees = carSteerLock.RoundToInt().Clamp(lockSetter.MinimumSteerLock, lockSetter.MaximumSteerLock);
                    Logging.Debug("Updated lock: " + newDegrees);

                    try {
                        if (lockSetter.Apply(newDegrees, false, out var appliedValue)) {
                            Logging.Write("Hardware lock applied successfully!");
                            steer.Set("__CM_ORIGINAL_LOCK", currentDegrees);
                            steer.Set("LOCK", appliedValue);
                            ini.Save();
                            return true;
                        }

                        Logging.Warning("Failed to apply hardware lock");
                    } catch (Exception e) {
                        NonfatalError.NotifyBackground("Failed to apply hardware lock", e);
                    }
                }

                goto End;
            }

            var autoAdjustScale = extra.GetBool("AUTO_ADJUST_SCALE", false);
            if (autoAdjustScale) {
                var currentScale = steer.GetDouble("SCALE", 1d);
                var newScale = Math.Min(currentDegrees / carSteerLock, 1d);
                if (newScale != currentScale) {
                    Logging.Debug($"Set scale: {newScale}");
                    steer.Set("__CM_ORIGINAL_SCALE", currentScale);
                    steer.Set("SCALE", newScale);
                    ini.Save();
                    return true;
                }
            }

            End:
            if (iniChanged) {
                ini.Save();
            }

            return specificControlsLoaded;
        }

        [CanBeNull]
        private static IWheelSteerLockSetter GetSteerLockSetter(IniFile ini) {
            var steer = ini["STEER"];
            var joy = ini["CONTROLLERS"].GetNonEmpty("PGUID" + steer.GetInt("JOY", -1));
            Logging.Debug($"Device GUID: {joy}");
            return WheelSteerLock.Get(joy);
        }

        private static bool Revert(IniFile ini, out int? originalLock) {
            var steer = ini["STEER"];
            originalLock = steer.GetIntNullable("__CM_ORIGINAL_LOCK");
            if (originalLock.HasValue) {
                steer.Set("LOCK", originalLock.Value);
                steer.Remove("__CM_ORIGINAL_LOCK");
            }

            var originalScale = steer.GetIntNullable("__CM_ORIGINAL_SCALE");
            if (originalScale.HasValue) {
                steer.Set("SCALE", originalScale.Value);
                steer.Remove("__CM_ORIGINAL_SCALE");
            }

            return originalLock.HasValue || originalScale.HasValue;
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}