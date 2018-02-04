using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Critical {
    [UsedImplicitly]
    public class MissingAeroLutRepair : CarSimpleRepairBase {
        [CanBeNull]
        private static string GetMissingLutName(DataWrapper data, IniFileSection section, string key) {
            var v = section.GetNonEmpty(key);
            if (v == null) return null;
            return !Lut.IsInlineValue(v) && (!data.Contains(v) || data.GetLutFile(v).Values.Count == 0) ? v : null;
        }

        protected override void Fix(CarObject car, DataWrapper data) {
            var aero = data.GetIniFile("aero.ini");
            foreach (var lut in aero.GetSections("WING").SelectMany(x => new[] {
                GetMissingLutName(data, x, "LUT_AOA_CD"),
                GetMissingLutName(data, x, "LUT_AOA_CL"),
            }).NonNull().Distinct().Select(data.GetLutFile)) {
                lut.Values.Clear();
                lut.Values.Add(new LutPoint(0, 0));
                lut.Save();
                Logging.Debug("Missing LUT created: " + lut.Name);
            }
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var aero = data.GetIniFile("aero.ini");
            var missingLuts = (from section in aero.GetSections("WING")
                               let cd = GetMissingLutName(data, section, "LUT_AOA_CD")
                               let cl = GetMissingLutName(data, section, "LUT_AOA_CL")
                               where cd != null || cl != null
                               select new {
                                   Name = section.GetNonEmpty("NAME"),
                                   Cd = cd,
                                   Cl = cl,
                                   Missing = (cd == null ? 0 : 1) + (cl == null ? 0 : 1)
                               }).ToList();
            if (missingLuts.Count == 0) return null;

            return new ContentObsoleteSuggestion(missingLuts.Select(x => x.Missing).Sum() > 1 ? "Empty aero LUTs" : "Empty aero LUT",
                    (missingLuts.Count == 1 ?
                            $"Wing {missingLuts[0].Name} doesn’t have valid {(missingLuts[0].Missing == 1 ? "LUT" : "LUTs")}." :
                            $"Wings {missingLuts.Select(x => x.Name).JoinToReadableString()} don’t have valid LUTs.") +
                            " It might cause AC to write tons of useless lines in log causing game to slow down and HDD to lost all space. And, if you use SSD… [url=\"http://i.imgur.com/6Y7hr0g.png\"]I was running it for a couple of minutes only, with a single car, and would you look at that.[/url]",
                    (p, c) => FixAsync(car, p, c)) { AffectsData = true };
        }
    }
}