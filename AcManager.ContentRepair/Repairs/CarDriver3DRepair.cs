using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Repairs {
    public class CarDriver3DRepair : CarSimpleRepairBase {
        [CanBeNull]
        private static string[] GetHiddenNodes(string driverModel) {
            switch (driverModel) {
                case "driver_60":
                    return new[] { "DRIVER:HELMET_69_SUB1", "DRIVER:HELMET_69_SUB0", "DRIVER:GEO_Driver_FACE" };
                case "driver_70":
                    return new[] {
                        "DRIVER:GEO_Driver_FACE", "DRIVER:HELMET_1975_SUB0",
                        "DRIVER:HELMET_1975_SUB1", "DRIVER:HELMET_1975_SUB2", "DRIVER:HELMET_1975_SUB3"
                    };
                case "driver_80":
                    return new[] { "DRIVER:HELMET1985", "DRIVER:GLASS_1985", "DRIVER:GEO_Driver_FACE" };
                case "driver":
                case "driver_no_HANS":
                    return new[] { "DRIVER:HELMET", "DRIVER:GEO_Driver_FACE" };
                default:
                    return null;
            }
        }

        protected override void Fix(CarObject car, DataWrapper data) {
            var driver = data.GetIniFile(@"driver3d.ini");
            var hidden = GetHiddenNodes(driver["MODEL"].GetNonEmpty("NAME"));
            if (hidden == null) return;

            driver.SetSections("HIDE_OBJECT", hidden.Union(driver.GetSections("HIDE_OBJECT").Select(x => x.GetNonEmpty("NAME"))).Select(x => new IniFileSection {
                ["NAME"] = x
            }));
            driver.Save();
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var driver = data.GetIniFile(@"driver3d.ini");
            var hidden = GetHiddenNodes(driver["MODEL"].GetNonEmpty("NAME"));
            if (hidden == null) return null;

            var carHidden = driver.GetSections("HIDE_OBJECT").Select(x => x.GetNonEmpty("NAME")).ToList();
            foreach (var s in hidden) {
                if (!carHidden.Contains(s)) {
                    goto Error;
                }
            }

            return null;

            Error:
            return new ContentObsoleteSuggestion("Incorrect driver params",
                    "When Kunos updated drivers models, they’ve changed names of some nodes as well, thus making some [mono]HIDE_OBJECT[/mono] sections invalid. Because of that, you might see some bits of driver’s face while driving.",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}