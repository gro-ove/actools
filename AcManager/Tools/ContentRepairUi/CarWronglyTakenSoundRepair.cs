using System.Collections.Generic;
using AcManager.ContentRepair;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentRepairUi {
    [UsedImplicitly]
    public class CarWronglyTakenSoundRepair : CarRepairBase {
        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            var soundDonor = car.GetSoundOrigin().ConfigureAwait(false).GetAwaiter().GetResult();
            if (soundDonor != null && soundDonor != "tatuusfa1") {
                var soundDonorObject = CarsManager.Instance.GetById(soundDonor);
                if (soundDonorObject != null) {
                    var thisLimiter = car.AcdData?.GetIniFile("engine.ini")["ENGINE_DATA"].GetFloat("LIMITER", 0) ?? 0;
                    var donorLimiter = soundDonorObject.AcdData?.GetIniFile("engine.ini")["ENGINE_DATA"].GetFloat("LIMITER", 0) ?? 0;
                    if (thisLimiter > 1000 && donorLimiter > 500 && donorLimiter <= thisLimiter - CarSoundReplacer.RpmLimiterThreshold) {
                        return new ContentRepairSuggestion[] {
                            new CommonErrorSuggestion("RPM limiter is way too different from sound origin’s one",
                                    $"Sound is taken from {soundDonorObject.DisplayName} with RPM limiter at {donorLimiter:F0}, but here limiter is at {thisLimiter:F0}. You might want to change sound to something more appropriate.",
                                    (progress, token) => CarSoundReplacer.Replace(car)) {
                                        ShowProgressDialog = false,
                                        AffectsData = false
                                    },
                        };
                    }
                }
            }

            return new ContentRepairSuggestion[0];
        }

        public override bool AffectsData => false;
    }
}