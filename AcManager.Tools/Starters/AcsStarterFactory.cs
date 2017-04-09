using System;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Starters {
    public static class AcsStarterFactory {
        [NotNull]
        private static IAcsStarter CreateFromSettings() {
            var type = SettingsHolder.Drive.SelectedStarterType;

            if (type == SettingsHolder.DriveSettings.OfficialStarterType) {
                return new OfficialStarter();
            }

            if (type == SettingsHolder.DriveSettings.UiModuleStarterType) {
                if (SettingsHolder.Drive.StarterFallbackIfNotAvailable && !ModuleStarter.IsAvailable()) {
                    return new OfficialStarter();
                }

                return new ModuleStarter();
            }

            if (type == SettingsHolder.DriveSettings.NaiveStarterType) {
                return new NaiveStarter();
            }

            if (type == SettingsHolder.DriveSettings.StarterPlusType) {
                return new StarterPlus();
            }

            if (type == SettingsHolder.DriveSettings.SseStarterType) {
                return new SseStarter();
            }

            if (type == SettingsHolder.DriveSettings.SidePassageStarterType) {
                return new SidePassageStarter();
            }

            if (type == SettingsHolder.DriveSettings.SteamStarterType) {
                return new SteamStarter();
            }

            if (type == SettingsHolder.DriveSettings.TrickyStarterType) {
                return new TrickyStarter(AcRootDirectory.Instance.Value) {
                    Use32Version = SettingsHolder.Drive.Use32BitVersion
                };
            }

            throw new ArgumentOutOfRangeException(nameof(SettingsHolder.Drive.SelectedStarterType));
        }

        private static void SetPlatform([NotNull] this IAcsStarter starter) {
            var p = starter as IAcsPlatformSpecificStarter;
            if (p != null) {
                p.Use32Version = SettingsHolder.Drive.Use32BitVersion;
            }
        }

        [NotNull]
        private static IAcsStarter CreateFallback() {
            return new TrickyStarter(AcRootDirectory.Instance.Value) {
                Use32Version = SettingsHolder.Drive.Use32BitVersion
            };
        }

        public static IAcsStarter Create() {
            var result = CreateFromSettings();
            result.SetPlatform();
            Logging.Debug($"Starter created: {result.GetType().Name}");
            
            var preparable = result as IAcsPrepareableStarter;
            if (preparable != null && !preparable.TryToPrepare()) {
                Logging.Warning("Can’t prepare, using fallback starter instead.");
                result = CreateFallback();
                result.SetPlatform();
            }

            return result;
        }
    }
}
