using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Critical {
    [UsedImplicitly]
    public class CarDefaultTyreSetRepair : CarSimpleRepairBase {
        public static readonly CarDefaultTyreSetRepair Instance = new CarDefaultTyreSetRepair();

        public static bool IsInvalid([NotNull] DataWrapper data) {
            var ini = data.GetIniFile(@"tyres.ini");
            var tyresCount = ini.GetExistingSectionNames("FRONT", -1).Count();
            var defaultIndex = ini["COMPOUND_DEFAULT"].GetInt("INDEX", 0);
            return defaultIndex >= tyresCount || defaultIndex < 0;
        }

        public static void Fix([NotNull] DataWrapper data) {
            var ini = data.GetIniFile(@"tyres.ini");
            ini["COMPOUND_DEFAULT"].Set("INDEX", 0);
            ini.Save();
        }

        protected override void Fix(CarObject car, DataWrapper data) {
            Fix(data);
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var ini = data.GetIniFile(@"tyres.ini");
            var tyresCount = ini.GetExistingSectionNames("FRONT", -1).Count();
            var defaultIndex = ini["COMPOUND_DEFAULT"].GetInt("INDEX", 0);

            if (defaultIndex >= tyresCount || defaultIndex < 0) {
                return new ContentObsoleteSuggestion("COMPOUND_DEFAULT/INDEX in tyres.ini is wrong",
                        tyresCount == 1 ?
                                $"There are only a single set of tyres available, but default set is {defaultIndex} (index is zero-based)" :
                                $"There are only {PluralizingConverter.PluralizeExt(tyresCount, "{0} set")} of tyres available, but default set is {defaultIndex} (index is zero-based)",
                        (p, c) => FixAsync(car, p, c)) {
                            AffectsData = true
                        };
            }

            return null;
        }
    }
}