using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.ContentRepair;
using AcManager.ContentRepair.Repairs;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentRepairUi {
    [UsedImplicitly]
    public class CarObsoleteSoundRepair : CarRepairBase {
        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            var guids = car.GuidsFilename;
            if (File.Exists(guids) && File.ReadAllText(guids).Contains("bus:/Wind and Wheels/wheels (open wheels)")) {
                return new ContentRepairSuggestion[] {
                    new ContentObsoleteSuggestion("Obsolete sound (possibly)",
                            "Judging by GUIDs, it appears that car won’t have an external sound in AC 1.9 and later. As a temporary solution, you could replace it with a sound of a Kunos car.",
                            (progress, token) => CarSoundReplacer.Replace(car)) {
                                ShowProgressDialog = false,
                                AffectsData = false
                            },
                };
            }

            return new ContentRepairSuggestion[0];
        }

        public override bool AffectsData => false;

        public override bool IsAvailable(IEnumerable<CarRepairBase> repairs) {
            return !repairs.OfType<CarObsoleteTakenSoundRepair>().Any();
        }

        public override double Priority => -1d;
    }
}