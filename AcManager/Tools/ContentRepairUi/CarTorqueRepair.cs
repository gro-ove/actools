using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AcManager.ContentRepair;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Selected;
using AcManager.Tools.Data;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ContentRepairUi {
    public class CarTorqueRepair : CarSimpleRepairBase {
        protected override Task<bool> FixAsync(CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));

            var data = car.AcdData;
            if (data == null || data.IsEmpty) return Task.FromResult(false);

            Lut torque, power;
            try {
                torque = TorquePhysicUtils.LoadCarTorque(data);
                power = TorquePhysicUtils.TorqueToPower(torque);
            } catch (Exception e) {
                Logging.Error(e);
                return Task.FromResult(false);
            }

            var multipler = ActionExtension.InvokeInMainThread(() => {
                var dlg = new CarTransmissionLossSelector(car, torque.MaxY, power.MaxY);
                dlg.ShowDialog();
                return dlg.IsResultOk ? dlg.Multipler : (double?)null;
            });

            if (!multipler.HasValue) return Task.FromResult(false);

            if (car.SpecsTorqueCurve != null) {
                var torqueUi = new Lut(car.SpecsTorqueCurve.Points);
                torqueUi.TransformSelf(x => x.Y * multipler.Value);
                car.SpecsTorqueCurve = new GraphData(torqueUi);
            }

            if (car.SpecsPowerCurve != null) {
                var powerUi = new Lut(car.SpecsPowerCurve.Points);
                powerUi.TransformSelf(x => x.Y * multipler.Value);
                car.SpecsPowerCurve = new GraphData(powerUi);
            }

            car.SpecsTorque = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_Torque_FormatTooltip,
                    (torque.MaxY * multipler.Value).ToString(@"F0", CultureInfo.InvariantCulture));
            car.SpecsBhp = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_Power_FormatTooltip,
                    (power.MaxY * multipler.Value).ToString(@"F0", CultureInfo.InvariantCulture));
            return Task.FromResult(true);
        }

        protected override void Fix(CarObject car, DataWrapper data) {}

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            // doesn’t work with KERS
            if (!data.GetIniFile("ers.ini").IsEmptyOrDamaged() || !data.GetIniFile("ctrl_ers_0.ini").IsEmptyOrDamaged()) {
                return null;
            }

            double maxUiTorque;
            if ((car.SpecsTorque?.IndexOf("*", StringComparison.Ordinal) ?? 0) != -1 ||
                    !FlexibleParser.TryParseDouble(car.SpecsTorque, out maxUiTorque)) {
                return null;
            }

            Lut torque;
            try {
                torque = TorquePhysicUtils.LoadCarTorque(data);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
            
            var loss = 1d - torque.MaxY / maxUiTorque;
            if (loss > 0.01) return null;

            var actual = loss.Abs() < 0.002 ? "same" : loss > 0 ? $"{loss:F1}% smaller" : $"{-loss:F1}% bigger";
            return new CommonErrorSuggestion("Suspiciously low transmission power loss",
                    $"Usually, in UI torque & power are taken from crankshaft, but data should contain torque at wheels, which is about 10–20% smaller. " +
                            $"Here, although, it’s {actual}. It might be a mistake.\n\nIf you want to specify weight without driver in UI, add “*”.",
                    (p, c) => FixAsync(car, p, c)) {
                        AffectsData = false,
                        ShowProgressDialog = false,
                        FixCaption = "Fix UI"
                    };
        }
    }
}