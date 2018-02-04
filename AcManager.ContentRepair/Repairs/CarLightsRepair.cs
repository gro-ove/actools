using System;
using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Repairs {
    [UsedImplicitly]
    public class CarLightsRepair : CarSimpleRepairBase {
        public void Multiply([NotNull] DataWrapper data, double multipler) {
            var lightsIni = data.GetIniFile("lights.ini");
            foreach (var section in lightsIni.GetSections("LIGHT")) {
                var value = section.GetVector3("COLOR");
                section.Set("COLOR", value.Select(x => x * multipler));
            }
            foreach (var section in lightsIni.GetSections("BRAKE")) {
                section.Set("COLOR", section.GetVector3("COLOR").Select(x => x * multipler));
                if (section.ContainsKey(@"OFF_COLOR")) {
                    section.Set("OFF_COLOR", section.GetVector3("OFF_COLOR").Select(x => x * multipler));
                }
            }
            lightsIni.Save();
        }

        protected override void Fix(CarObject car, DataWrapper data) {
            var lights = data.GetIniFile(@"lights.ini");
            if (lights == null) return;

            var max = lights.GetSections("LIGHT").Select(x => {
                if (x.GetNonEmpty("NAME") == "NULL") return 0d;

                var color = x.GetVector3("COLOR");
                return color.Max();
            }).MaxOrDefault();
            Multiply(data, max <= 0d ? 5d : Math.Max(5d, 32d / max));
        }

        private static bool CheckIfObsolete(DataWrapper data) {
            var lights = data.GetIniFile(@"lights.ini");
            if (lights == null) return false;

            var found = false;
            if (lights.GetSections("LIGHT").Concat(lights.GetSections("BRAKE")).Any(x => {
                if (x.GetNonEmpty("NAME") == "NULL") return false;

                var color = x.GetVector3("COLOR");
                if (color.All(y => Equals(y, 0d))) return false;

                found = true;
                return color.Any(y => y > 30d);
            }) || !found) return false;

            return true;
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            if (!CheckIfObsolete(data)) return null;
            return new ContentObsoleteSuggestion("Lights might be too dim",
                    "When Kunos changed HDR parameters, values in [mono]lights.ini[/mono] were increased in several times.",
                    (p, c) => FixAsync(car, p, c)) {
                AffectsData = true
            };
        }
    }
}