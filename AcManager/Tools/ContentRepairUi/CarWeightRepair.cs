using System;
using System.Globalization;
using System.Threading.Tasks;
using AcManager.ContentRepair;
using AcManager.Pages.Selected;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.ContentRepairUi {
    public class CarWeightRepair : CarSimpleRepairBase {
        public static double OptionAllowedPadding = 50;

        protected override void Fix(CarObject car, DataWrapper data) {
            double uiWeight;
            if ((car.SpecsWeight?.IndexOf("*", StringComparison.Ordinal) ?? 0) != -1 ||
                    !FlexibleParser.TryParseDouble(car.SpecsWeight, out uiWeight)) return;
            data.GetIniFile(@"car.ini")["BASIC"].Set("TOTALMASS", uiWeight + CommonAcConsts.DriverWeight);
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            if ((car.SpecsWeight?.IndexOf("*", StringComparison.Ordinal) ?? 0) != -1) return null;

            var uiWeight = car.GetWidthValue() ?? 0d;
            if (uiWeight <= 1d) return null;

            var withDriver = data.GetIniFile(@"car.ini")["BASIC"].GetFloat("TOTALMASS", -1f);
            if (withDriver <= 0f) return null;

            var weight = withDriver - CommonAcConsts.DriverWeight;
            var driverWeight = withDriver - uiWeight;
            if (driverWeight > CommonAcConsts.DriverWeight - OptionAllowedPadding) return null;

            var valueStr = Math.Abs(driverWeight) < 0.1 ? "nothing" : driverWeight < 1 ? $"{driverWeight} kg" : $"only {driverWeight} kg";
            return new CommonErrorSuggestion("Invalid weight",
                    $"In car.ini, TOTALMASS should include driver weight (+{CommonAcConsts.DriverWeight} kg) as well, " +
                            $"but according to these TOTALMASS and mass in UI file, driver weights {valueStr}. Could be a mistake.\n\nIf you want to specify weight with driver in UI, add “*”.",
                    (p, c) => FixAsync(car, p, c)) {
                        AffectsData = true,
                        FixCaption = "Fix data"
                    }.AlternateFix("Fix UI", (progress, token) => {
                        car.SpecsWeight = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_Weight_FormatTooltip,
                                weight.ToString(@"F0", CultureInfo.InvariantCulture));
                        return Task.FromResult(true);
                    }, false).AlternateFix("Add “*”", (progress, token) => {
                        car.SpecsWeight = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_Weight_FormatTooltip,
                                withDriver.ToString(@"F0", CultureInfo.InvariantCulture)) + "*";
                        return Task.FromResult(true);
                    }, false);
        }
    }
}