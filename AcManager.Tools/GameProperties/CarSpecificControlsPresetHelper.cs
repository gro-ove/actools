using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SharedMemory;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.WheelAngles;
using AcTools.WheelAngles.Implementations.Options;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

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

            if (iniChanged) {
                ini.Save();
            }
            new TemporaryFileReplacement(null, controlsFilename, BackupPostfix).Revert();
        }

        protected override bool SetOverride(CarObject car) {
            var controlsFilename = AcPaths.GetCfgControlsFilename();
            var specificControlsLoaded = car.SpecificControlsPreset && car.ControlsPresetFilename != null
                    && new TemporaryFileReplacement(car.ControlsPresetFilename, controlsFilename, BackupPostfix, false).Apply(true);

            var ini = new IniFile(controlsFilename);
            var iniChanged = Revert(ini, out _);

            var extra = ini["__EXTRA_CM"];
            if (specificControlsLoaded 
                || extra.GetBool("CAR_SPECIFIC", true) // so it’s not set by Presets Per Mode
                && extra.ContainsKey("PRESET_OVERRIDE")) {
                extra.SetOrRemove("CAR_SPECIFIC", specificControlsLoaded ? "1" : null);
                extra.SetOrRemove("PRESET_OVERRIDE", specificControlsLoaded ? JsonConvert.SerializeObject(car.ControlsPresetFilename) : null);
                iniChanged = true;
            }

            var steer = ini["STEER"];

            var carSteerLock = car.SteerLock ?? 0d;
            if (carSteerLock < 40) {
                Logging.Warning("Invalid car steer lock");
                Revert();
                return specificControlsLoaded;
            }

            Logging.Write($"Car steer lock: {carSteerLock}°");
            var hardLock = extra.GetBool("HARDWARE_LOCK", false);
            var currentDegrees = steer.GetInt("LOCK", 900);

            if (hardLock) {
                Logging.Write($"Hardware lock enabled, controls lock: {currentDegrees}");
            }

            if (hardLock && currentDegrees > carSteerLock) {
                var lockSetter = GetSteerLockSetter(ini);
                Logging.Write(lockSetter == null
                        ? "Lock setter for provided device not found"
                        : $"Lock setter: {lockSetter.MinimumSteerLock}…{lockSetter.MaximumSteerLock}");

                if (lockSetter != null && carSteerLock < lockSetter.MaximumSteerLock) {
                    var newDegrees = carSteerLock.RoundToInt().Clamp(lockSetter.MinimumSteerLock, lockSetter.MaximumSteerLock);
                    Logging.Write("Updated lock: " + newDegrees);

                    try {
                        if (lockSetter.Apply(newDegrees, false, out var appliedValue)) {
                            Logging.Write("Hardware lock applied successfully!");
                            steer.Set("__CM_ORIGINAL_LOCK", currentDegrees);
                            steer.Set("LOCK", appliedValue);
                            ini.Save();

                            if (lockSetter.GetOptions() is IGameWaitingWheelOptions waiting) {
                                SpecialLogitechFix(waiting);
                            }

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
                if (newScale != Math.Abs(currentScale)) {
                    Logging.Write($"Set scale: {newScale}");
                    steer.Set("__CM_ORIGINAL_SCALE", currentScale);
                    steer.Set("SCALE", newScale * Math.Sign(currentScale));
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

        private void SpecialLogitechFix(IGameWaitingWheelOptions logitech) {
            if (SettingsHolder.Drive.WatchForSharedMemory) {
                AcSharedMemory.Instance.Start += OnStart;
            } else {
                Task.Delay(5000).ContinueWith(t => logitech.OnGameStarted());
            }

            void OnStart(object sender, EventArgs eventArgs) {
                AcSharedMemory.Instance.Start -= OnStart;
                logitech.OnGameStarted();
            }
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

            var originalOrigin = ini["__EXTRA_CM"].GetNonEmpty("PRESET_OVERRIDE");
            if (originalOrigin != null) {
                ini["__EXTRA_CM"].Remove("PRESET_OVERRIDE");
            }

            return originalLock.HasValue || originalScale.HasValue || originalOrigin != null;
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}