using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.Tools.GameProperties {
    public class CarSpecificFanatecSettingsHelper : CarSpecificHelperBase {
        private const string BackupPostfix = "_backup_cm_carspec";

        public static void Revert() {
            var filename = AcSettingsHolder.Fanatec.Filename;
            var ini = new IniFile(filename);
            var anyChange = false;
            if (ini["SETTINGS"].ContainsKey("__CM_ORIGINAL_ENABLED")) {
                ini["SETTINGS"].Set("ENABLED", ini["SETTINGS"].GetNonEmpty("__CM_ORIGINAL_ENABLED"));
                ini["SETTINGS"].Remove("__CM_ORIGINAL_ENABLED");
                anyChange = true;
            }
            if (ini["SETTINGS"].ContainsKey("__CM_ORIGINAL_GEAR_MAX_TIME")) {
                ini["SETTINGS"].Set("GEAR_MAX_TIME", ini["SETTINGS"].GetNonEmpty("__CM_ORIGINAL_GEAR_MAX_TIME"));
                ini["SETTINGS"].Remove("__CM_ORIGINAL_GEAR_MAX_TIME");
                anyChange = true;
            }
            if (anyChange) {
                ini.Save();
            }
        }

        protected override bool SetOverride(CarObject car) {
            if (!car.FanatecCustomSettings || !AcSettingsHolder.Fanatec.AllowToOverridePerCar) return false;
            var filename = AcSettingsHolder.Fanatec.Filename;
            var ini = new IniFile(filename);
            if (!ini["SETTINGS"].ContainsKey("__CM_ORIGINAL_ENABLED")) {
                ini["SETTINGS"].Set("__CM_ORIGINAL_ENABLED", ini["SETTINGS"].GetNonEmpty("ENABLED"));
            }
            if (!ini["SETTINGS"].ContainsKey("__CM_ORIGINAL_GEAR_MAX_TIME")) {
                ini["SETTINGS"].Set("__CM_ORIGINAL_GEAR_MAX_TIME", ini["SETTINGS"].GetNonEmpty("GEAR_MAX_TIME"));
            }
            ini["SETTINGS"].Set("ENABLED", !car.FanatecDisable);
            if (car.FanatecOnlyShowGear) {
                ini["SETTINGS"].Set("GEAR_MAX_TIME", 99999d);
            } else {
                ini["SETTINGS"].Set("GEAR_MAX_TIME", ini["SETTINGS"].GetNonEmpty("__CM_ORIGINAL_GEAR_MAX_TIME"));
            }
            ini.Save();
            return true;
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}