using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.ContentRepair {
    public class CarAiUpRepair : CarRepairBase {
        protected Task<bool> FixAsync([NotNull] CarObject car, Action<DataWrapper> action, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));
            return Task.Run(() => {
                var data = car.AcdData;
                if (data == null || data.IsEmpty) return false;
                action(data);
                return true;
            });
        }

        private void FixAiIni(DataWrapper data) {
            var limiter = data.GetIniFile("engine.ini")["ENGINE_DATA"].GetInt("LIMITER", 0);
            if (limiter < 1000) return;

            var aiIni = data.GetIniFile("ai.ini");
            aiIni["GEARS"].Set("UP", limiter - 300);
            aiIni.Save();
        }

        private void FixDrivetrainIni(DataWrapper data) {
            var limiter = data.GetIniFile("engine.ini")["ENGINE_DATA"].GetInt("LIMITER", 0);
            if (limiter < 1000) return;

            var drivetrainIni = data.GetIniFile("drivetrain.ini");
            drivetrainIni["AUTO_SHIFTER"].Set("UP", limiter - 300);
            drivetrainIni.Save();
        }

        public override IEnumerable<ObsoletableAspect> GetObsoletableAspects(CarObject car) {
            var data = car.AcdData;
            if (data == null || data.IsEmpty) yield break;


            var limiter = data.GetIniFile("engine.ini")["ENGINE_DATA"].GetInt("LIMITER", 0);
            if (limiter < 1000) yield break;

            {
                var aiIni = data.GetIniFile("ai.ini");
                var aiUp = aiIni["GEARS"].GetInt("UP", 0);
                if (aiUp == 0 || aiUp > limiter) {
                    yield return new ObsoletableAspect("Value [mono]GEARS/UP[/mono] in ai.ini might be incorrect",
                            "It’s either missing or haven’t set properly.",
                            (p, c) => FixAsync(car, FixAiIni, p, c)) {
                        AffectsData = true
                    };
                }
            }

            {
                var drivetrainIni = data.GetIniFile("drivetrain.ini");
                var drivetrainUp = drivetrainIni["AUTO_SHIFTER"].GetInt("UP", 0);
                if (drivetrainUp == 0 || drivetrainUp > limiter) {
                    yield return new ObsoletableAspect("Value [mono]AUTO_SHIFTER/UP[/mono] in drivetrain.ini might be incorrect",
                            "It’s either missing or haven’t set properly, it might affect automatic gearbox.",
                            (p, c) => FixAsync(car, FixDrivetrainIni, p, c)) {
                        AffectsData = true
                    };
                }
            }
        }

        public override bool AffectsData => true;
    }
}